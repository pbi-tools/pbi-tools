/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Serilog;
using Spectre.Console;

namespace PbiTools.Deployments
{
    using Options = PbiDeploymentOptions.DatasetOptions.GatewayOptions;

    public class DatasetGatewayManager
    { 
        private static readonly ILogger Log = Serilog.Log.ForContext<DatasetGatewayManager>();

        private readonly Options _options;
		private readonly PbiDeploymentOptions.ConsoleOptions _consoleOptions;
        private readonly IPowerBIClient _powerBI;

        public DatasetGatewayManager(Options options, PbiDeploymentOptions.ConsoleOptions consoleOptions, IPowerBIClient powerBIClient, bool createdNewDb)
        {
            _options = options;
            _consoleOptions = consoleOptions ?? throw new ArgumentNullException(nameof(consoleOptions));
            _powerBI = powerBIClient ?? throw new ArgumentNullException(nameof(powerBIClient));

            Enabled = options != null
                && ((options.Mode == Options.GatewayBindMode.OnCreation && createdNewDb) || (options.Mode == Options.GatewayBindMode.Always))
                && (options.GatewayId != default || options.DiscoverGateways);
        }

        public bool WhatIf { get; set; }

        public bool Enabled { get; }

        /// <summary>
        /// Discovers and reports the gateways the dataset can be bound to.
        /// </summary>
        public async Task DiscoverGatewaysAsync(Guid workspaceId, string datasetId)
        {
            if (!Enabled || !_options.DiscoverGateways || datasetId == default) return;

            var gateways = await _powerBI.Datasets.DiscoverGatewaysInGroupAsync(workspaceId, datasetId);

            var table = new Spectre.Console.Table { Expand = _consoleOptions.ExpandTable };

            table.AddColumns("ID", "Name", "Type");

            foreach (var item in gateways.Value)
            {
                table.AddRow(
                    item.Id.ToString(),
                    item.Name.EscapeMarkup(),
                    item.Type.ToString()
                );
            }

            Log.Information("Discovered Gateways:");

            AnsiConsole.Write(table);
        }

        /// <summary>
        /// If gateway binding is enabled, binds the dataset to the specified gateway.
        /// Does nothing in WhatIf mode.
        /// </summary>
        /// <returns>True if the dataset was bound to a gateway.</returns>
        public async Task<bool> BindToGatewayAsync(Guid workspaceId, string datasetId, IDictionary<string, DeploymentParameter> parameters)
        {
            if (!Enabled) return false;

            if (!WhatIf && _options.GatewayId != default)
            {
                if (!Guid.TryParse(_options.GatewayId.ExpandParamsAndEnv(parameters), out var gatewayId))
                    throw new DeploymentException($"The GatewayId expression could not be resolved as a valid Guid: {_options.GatewayId}.");

                Log.Debug("Binding dataset to gateway: {GatewayId}", gatewayId);

                await _powerBI.Datasets.BindToGatewayInGroupAsync(workspaceId, datasetId,
                    new BindToGatewayRequest(
                        gatewayId,
                        ResolveDatasetSources(gatewayId, _options.DataSources, _powerBI)
                ));

                Log.Information("Successfully bound dataset to gateway: {GatewayId} ({GatewayBindMode})", gatewayId, _options.Mode);

                return true;
            }

            return false;
        }

        private static IList<Guid?> ResolveDatasetSources(Guid gatewayId, string[] datasources, IPowerBIClient powerBI)
        {
            if (datasources == null || datasources.Length == 0) return default;

            var gatewaySources = new Lazy<IList<GatewayDatasource>>(() => 
            {
                Log.Debug("Fetching datasources for gateway: {GatewayID}", gatewayId);
                var result = powerBI.Gateways.GetDatasources(gatewayId);
                return result.Value;
            });

            var sources = datasources.Aggregate(
                new List<Guid?>(),
                (list, value) => {
                    if (Guid.TryParse(value, out var id))
                    {
                        list.Add(id);
                    }
                    else
                    {
                        var match = gatewaySources.Value.FirstOrDefault(x =>
                            x.DatasourceName.Equals(value, StringComparison.InvariantCultureIgnoreCase)
                        );

                        if (match == null) {
                            throw new DeploymentException($"Failed to lookup datasource ID for {value} on gateway: {gatewayId}.");
                        }

                        Log.Debug("Resolved datasource '{DatasourceName}' as {DatasourceID}.", value, match.Id);
                        list.Add(match.Id);
                    }
                    return list;
                }
            );

            return sources;
        }

    }

}
