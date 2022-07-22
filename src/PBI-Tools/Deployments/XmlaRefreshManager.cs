﻿using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using WildcardMatch;
using AMO = Microsoft.AnalysisServices;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.Deployments
{
	public class XmlaRefreshManager
	{
        private static readonly ILogger Log = Serilog.Log.ForContext<XmlaRefreshManager>();

        private readonly TOM.Database _database;

        public XmlaRefreshManager(TOM.Database database)
        {
            this._database = database ?? throw new ArgumentNullException(nameof(database));
        }


        public string BasePath { get; set; }

        public PbiDeploymentOptions.RefreshOptions ManifestOptions { get; set; }

        public PbiDeploymentEnvironment.RefreshOptions EnvironmentOptions { get; set; }

        public void RunRefresh()
        {
            _database.Model.Sync();

            var refreshables = CalculateRefreshables(GetPartitions(_database.Model), ManifestOptions, EnvironmentOptions);
            foreach (var refreshable in refreshables)
            {
                switch (refreshable.ObjectType)
                {
                    case RefreshObjectType.Model:
                        Log.Information("Refreshing model ({RefreshType})...", refreshable.RefreshType);
                        _database.Model.RequestRefresh(refreshable.RefreshType);
                        break;
                    case RefreshObjectType.Table:
                        Log.Information("Refreshing table {Table} ({RefreshType})...", refreshable.Table, refreshable.RefreshType);
                        _database.Model.Tables[refreshable.Table].RequestRefresh(refreshable.RefreshType);
                        break;
                    case RefreshObjectType.Partition:
                        Log.Information("Refreshing partition {Table}|{Partition} ({RefreshType})...", refreshable.Table, refreshable.Partition, refreshable.RefreshType);
                        _database.Model.Tables[refreshable.Table].Partitions[refreshable.Partition].RequestRefresh(refreshable.RefreshType);
                        break;
                }
            }

            using var trace = new XmlaRefreshTrace(_database.Server, ManifestOptions.Tracing ?? new(), BasePath);
            trace.Start();

            try
            {
                var refreshResults = _database.Model.SaveChanges();

                if (refreshResults.XmlaResults != null && refreshResults.XmlaResults.Count > 0)
                {
                    Log.Information("Refresh Results:");

                    foreach (var result in refreshResults.XmlaResults.OfType<AMO.XmlaResult>())
                    {
                        Log.Information(result.Value);
                        foreach (var message in result.Messages.OfType<AMO.XmlaMessage>())
                            Log.Warning("- [{Severity}] {Description}\n\t{Location}\n--", message.GetType().Name, message.Description, message.Location?.SourceObject);
                    }
                }
            }
            catch (AMO.OperationException ex) when (ex.Message.Contains("DMTS_DatasourceHasNoCredentialError"))
            {
                throw new DeploymentException("Refresh failed because of missing credentials. See https://docs.microsoft.com/power-bi/enterprise/service-premium-connect-tools#setting-data-source-credentials for further details.", ex);
            }

        }

        public static ModelPartition[] GetPartitions(TOM.Model model) =>
            model.Tables
                .SelectMany(t => t.Partitions)
                .Select(p => new ModelPartition(p.Table.Name, p.Name))
                .ToArray();

        public static ModelRefreshable[] CalculateRefreshables(ModelPartition[] partitions,
            PbiDeploymentOptions.RefreshOptions manifestOptions,
            PbiDeploymentEnvironment.RefreshOptions environmentOptions)
        {
            var hasEnvObjects = environmentOptions != null
                && !environmentOptions.Skip
                && environmentOptions.Objects != null
                && !environmentOptions.Objects.IsEmpty;

            if ((manifestOptions.Objects == null || manifestOptions.Objects.IsEmpty)
                && !hasEnvObjects)
            {
                Log.Debug("No refresh objects specified. Entire model will be refreshed.");
                return new[] { new ModelRefreshable(RefreshObjectType.Model, manifestOptions.Type.ConvertToTOM()) };
            }

            var allPartitions = new List<ModelPartition>(partitions);
            var results = new List<ModelRefreshable>();

            if (hasEnvObjects)
                Log.Debug("Processing {ExpressionCount} refresh object expressions from current environment.", environmentOptions.Objects.Count());
            else
                Log.Debug("Processing {ExpressionCount} refresh object expressions from current environment.", manifestOptions.Objects.Count());

            foreach (var refreshObj in (hasEnvObjects ? environmentOptions.Objects : manifestOptions.Objects))
            {
                Log.Debug("Applying refresh object expression: {Expression}", refreshObj.OriginalString);

                if (refreshObj.ObjectType == RefreshObjectType.Table)
                {
                    // Identify remaining tables matching current expression
                    var selectedTables = allPartitions
                        .GroupBy(p => p.Table)
                        .Where(g => g.Key.WildcardMatch(refreshObj.TableExpression))
                        .Select(g => g.Key)
                        .ToArray();

                    // Add tables to refreshables, unless 'None' selected
                    if (refreshObj.RefreshType.HasValue)
                        Array.ForEach(selectedTables, t =>
                        {
                            Log.Verbose("Requesting {RefreshType} for table {Table}.", refreshObj.RefreshType.Value, t);
                            results.Add(new ModelRefreshable(RefreshObjectType.Table, refreshObj.RefreshType.Value.ConvertToTOM(), t));
                        });

                    // Remove all partitions belonging to matching tables from initial list
                    allPartitions.RemoveAll(p => p.Table.WildcardMatch(refreshObj.TableExpression));
                }
                else if (refreshObj.ObjectType == RefreshObjectType.Partition)
                {
                    // Identify remaining tables matching current expression
                    var selectedPartitions = allPartitions
                        .Where(p => p.ToString().WildcardMatch(refreshObj.OriginalString))
                        .ToArray();

                    // Add partitions to refreshables, unless 'None' selected
                    if (refreshObj.RefreshType.HasValue)
                        Array.ForEach(selectedPartitions, p =>
                        {
                            Log.Verbose("Requesting {RefreshType} for partition {Table}|{Partition}.", refreshObj.RefreshType.Value, p.Table, p.Partition); 
                            results.Add(new ModelRefreshable(RefreshObjectType.Partition, refreshObj.RefreshType.Value.ConvertToTOM(), p.Table, p.Partition));
                        });

                    // Remove all selected partitions from initial list
                    Array.ForEach(selectedPartitions, p => allPartitions.Remove(p));
                }
            }

            // Handle remaining partitions (using manifest type)
            foreach (var p in allPartitions)
            {
                results.Add(new ModelRefreshable(RefreshObjectType.Partition, 
                    manifestOptions.Type.ConvertToTOM(), 
                    p.Table, 
                    p.Partition));
            }

            return results.ToArray();            
        }

    }

    public struct ModelPartition
    {
        public ModelPartition(string table, string partition)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            Partition = partition ?? throw new ArgumentNullException(nameof(partition));
        }

        public string Table { get; }
        public string Partition { get; }

        public override string ToString() => $"{Table}|{Partition}";
    }

    public struct ModelRefreshable
    {
        public ModelRefreshable(RefreshObjectType objectType, TOM.RefreshType refreshType, string table = null, string partition = null)
        {
            ObjectType = objectType;
            RefreshType = refreshType;
            Table = table;
            Partition = partition;
        }

        public RefreshObjectType ObjectType { get; }
        public string Table { get; set; }
        public string Partition { get; set; }
        public TOM.RefreshType RefreshType { get; }
    }

}