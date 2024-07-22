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
using System.IO;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PbiTools.PowerBI
{
    /// <summary>
    /// Allows generating a custom connections file for a Power BI report with a live connection.
    /// </summary>
    public class ReportConnection
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ReportConnection>();

        public const string DatabaseIdParameter = "{{DATASET_ID}}";
        private const string DefaultConnectionsTemplate = "Connections.pbiServiceLive.json";

        internal string Template { get; private set; }

        /// <summary>
        /// Creates a <see cref="ReportConnection"/> instance using the default template included in <c>pbi-tools</c>.
        /// </summary>
        public static ReportConnection CreateDefault()
        {
            Log.Information("Generating report connections file from embedded default template.");
            return new() { Template = Utils.Resources.GetEmbeddedResourceString(DefaultConnectionsTemplate) };
        }

        /// <summary>
        /// Creates a <see cref="ReportConnection"/> instance using a custom template. Http and File sources are supported.
        /// The template must contain the <c>{{DATASET_ID}}</c> parameter.
        /// </summary>
        /// <param name="templatePath">A url to GET the template from, if the path starts with "http", otherwise a (relative) file path.</param>
        /// <param name="basePath">If <c>templatePath</c> is resolved as a file path, specifies the (optional) base path against which
        /// the template path will be resolved. Defaults to the current working directory otherwise.</param>
        public static ReportConnection Create(string templatePath, string basePath = default)
        {
            if (string.IsNullOrWhiteSpace(templatePath)) throw new ArgumentNullException(nameof(templatePath));

            if (templatePath.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) {
                Log.Information("Generating report connections file from HTTP source: {Url}", templatePath);
                using var http = new HttpClient();
                // TODO Support authentication and custom headers
                var template = http.GetStringAsync(templatePath).Result;

                return new() { Template = template };
            }

            var filePath = Path.Combine(basePath ?? Directory.GetCurrentDirectory(), templatePath);
            if (!File.Exists(filePath))
                throw new FileNotFoundException("The template file does not exist at the specified path.", filePath);

            Log.Information("Generating report connections file from file source: {Path}", filePath);
            return new() { Template = File.ReadAllText(filePath) };
        }

        /// <summary>
        /// Returns the connections file as a <see cref="JObject"/>.
        /// The <c>databaseId</c> is filled into the template in place of the <c>{{DATASET_ID}}</c> parameter.
        /// </summary>
        public JObject ToJson(string databaseId)
        {
            Log.Debug("Generating a connections file with Database ID: {DatabaseId}", databaseId);

            var json = JObject.Parse(Template.Replace(DatabaseIdParameter, databaseId ?? throw new ArgumentNullException(nameof(databaseId))));

            Log.Verbose("Generated connections file:\n{Json}", json);

            return json;
        }

    }
}
