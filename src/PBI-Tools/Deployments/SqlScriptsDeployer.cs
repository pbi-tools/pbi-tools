// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using DbUp;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Engine.Transactions;
using DbUp.Helpers;
using DbUp.ScriptProviders;
using Serilog;

namespace PbiTools.Deployments
{
    using Utils;
    using Options = PbiDeploymentOptions.SqlScriptsOptions;

    public class SqlScriptsDeployer
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<SqlScriptsDeployer>();

        public static string ArtifactsSubfolder = "sqlScripts";

        private readonly Options _options;
        private readonly DeploymentParameters _parameters;
        private readonly string _artifactsPath;
        private readonly string _environment;
        private readonly string _basePath;
        private readonly Lazy<string> _connectionString;
        private readonly string _schema;

        public SqlScriptsDeployer(Options options, DeploymentParameters parameters, DeploymentArtifactsFolder artifactsFolder, string environment, string basePath)
        {
            this._options = options ?? throw new ArgumentNullException(nameof(options));
            this._parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            this._artifactsPath = artifactsFolder.GetSubfolder(ArtifactsSubfolder).FullName;
            this._environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this._basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));

            this._connectionString = new Lazy<string>(() => CreateConnectionString(options.Connection, parameters));
            this._schema = options.Schema.ExpandParamsAndEnv(parameters);

            if (options.Enabled)
            {
                Log.Information("Deploying SqlScripts to schema: {Schema}", _schema);
                Log.Information("Using SqlScripts artifacts folder: {SqlScriptsArtifactsFolder}", _artifactsPath);
            }
        }

        internal static string CreateConnectionString(IDictionary<string, string> connectionArgs, DeploymentParameters parameters)
        {
            if (connectionArgs.TryGetValue(nameof(DbConnectionStringBuilder.ConnectionString), out var value)) {
                return value.ExpandParamsAndEnv(parameters);
            }

            var bldr = new DbConnectionStringBuilder();
            foreach (var item in connectionArgs)
                bldr.Add(item.Key, item.Value.ExpandParamsAndEnv(parameters));

            return bldr.ConnectionString;
        }

        public bool WhatIf { get; set; }

        private DbUp.Builder.UpgradeEngineBuilder GetUpgradeEngineBuilder()
            => DeployChanges.To.SqlDatabase(_connectionString.Value, _schema);

        public void TestConnection()
        {
            if (!_options.Enabled) return;

            Log.Debug("Trying to connect to SQL database...");
            var engine = GetUpgradeEngineBuilder()
                .WithScript("dummy", "-- Do Nothing") // 'TryConnect()' requires a ScriptProvider
                .Build();
            if (!engine.TryConnect(out var errorMessage)) {
                throw new DeploymentException($"Failed to connect to the SqlScripts database: {errorMessage}");
            }
            Log.Information("Tested SQL database connection successfully.");
        }

        public void EnsureDatabase()
        {
            if (WhatIf || !_options.Enabled || !_options.EnsureDatabase) return;

            // TODO Log

            DbUp.EnsureDatabase
                .For.SqlDatabase(_connectionString.Value);
        }

        private UpgradeEngine BuildUpgradeEngine(Func<UpgradeEngineBuilder, UpgradeEngineBuilder> callback) 
        {
            // engineBuilder
            var engineBuilder = GetUpgradeEngineBuilder();
            // Log
            engineBuilder = engineBuilder.LogToAutodetectedLog();
            if (_options.LogScriptOutput) {
                engineBuilder = engineBuilder.LogScriptOutput();
            }
            // Journal
            engineBuilder = engineBuilder.JournalTo(new NullJournal());
            // Transaction TODO

            // Variables
            engineBuilder = engineBuilder.WithVariables(_parameters.ToDictionary(
                x => x.Key,
                x => x.Value.ToString()
            ));

            engineBuilder = callback(engineBuilder);

            return engineBuilder.Build();
        }

        private void RunPipelineScript(string scriptName)
        {
            if (!_options.Enabled) return;

            if (!TryGetArtifactAsString(scriptName, out var script))
                return;

            var engine = BuildUpgradeEngine(engineBuilder => 
                engineBuilder.WithScript(new SqlScript(
                    scriptName, 
                    script,
                    new() { ScriptType = DbUp.Support.ScriptType.RunAlways }
                )));

            if (WhatIf) {
                var scriptsToExecute = engine.GetScriptsToExecute();
                // TODO .................
            }
            else
            {
                if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug)) {
                    var scriptsToExecute = engine.GetScriptsToExecute();
                    foreach (var _script in scriptsToExecute) {
                        Log.Debug("Executing script: {Name}\n{ScriptContent}", _script.Name, _script.Contents);
                    }
                }

                var results = engine.PerformUpgrade();

                if (!results.Successful) {
                    throw new DeploymentException($"SqlScripts deployment failed: {results.Error.Message}", results.Error);
                }
            }
        }

        public void RunBeforeUpdate() => RunPipelineScript("beforeUpdate.sql");

        public void RunAfterUpdate() => RunPipelineScript("afterUpdate.sql");

        public void RunSqlScripts()
        {
            if (!_options.Enabled) return;

            var engine = BuildUpgradeEngine(engineBuilder => 
            {
                // Journal
                if (_options.Journal != null) {
                    engineBuilder = engineBuilder.JournalToSqlTable(
                        _options.Journal.Schema.ExpandParamsAndEnv(_parameters),
                        _options.Journal.Table.ExpandParamsAndEnv(_parameters)
                    );
                }

                // Scripts
                var sqlScriptFolder = new DirectoryInfo(Path.Combine(_basePath, _options.Path));
                if (!sqlScriptFolder.Exists) {
                    throw new DeploymentException($"SqlScripts path '{_options.Path}' does not exist at: {sqlScriptFolder.FullName}.");
                }
                TryGetArtifactAsString("template.sql", out var template);

                return engineBuilder.WithScripts(new SqlScriptsProvider(sqlScriptFolder.FullName, template));
            });

            if (_options.HtmlReportPath.HasValue()) {
                var htmlPath = new FileInfo(Path.Combine(_basePath, _options.HtmlReportPath));
                Log.Debug("Generating SqlScripts HTML report at: {Path}", htmlPath.FullName);
                engine.GenerateUpgradeHtmlReport(htmlPath.FullName);
                Log.Information("Generated SqlScripts HTML report at: {Path}", htmlPath.FullName);
            }

            if (WhatIf) {
                var scriptsToExecute = engine.GetScriptsToExecute();
                // TODO .................
            }
            else
            {
                var results = engine.PerformUpgrade();

                if (!results.Successful) {
                    throw new DeploymentException($"SqlScripts deployment failed: {results.Error.Message}", results.Error);
                }
            }
        }

        public void RunAfterRefresh() => RunPipelineScript("afterRefresh.sql");


        public bool TryGetArtifactAsString(string path, out string contents) 
        {
            var originalExtension = Path.GetExtension(path);
            var pathWithEnvironment = Path.ChangeExtension(path, $".{_environment}{originalExtension}");

            var fileInfo = new FileInfo(Path.Combine(_artifactsPath, pathWithEnvironment));
            if (fileInfo.Exists) {
                Log.Debug("Reading artifacts file: {path}", fileInfo.FullName);
                contents = File.ReadAllText(fileInfo.FullName);
                return true;
            }

            fileInfo = new FileInfo(Path.Combine(_artifactsPath, path));
            if (fileInfo.Exists) {
                Log.Debug("Reading artifacts file: {path}", fileInfo.FullName);
                contents = File.ReadAllText(fileInfo.FullName);
                return true;
            }

            Log.Debug("Artifacts file does not exist: {path}", fileInfo.FullName);
            contents = default;
            return false;
        }

        public class SqlScriptsProvider : IScriptProvider
        {
            private readonly IScriptProvider _inner;
            private readonly string _template;

            public SqlScriptsProvider(string directoryPath, string template)
            {
                _inner = new FileSystemScriptProvider(
                    directoryPath ?? throw new ArgumentNullException("directoryPath"),
                    new() { IncludeSubDirectories = true, UseOnlyFilenameForScriptName = false },
                    new() {  }
                );
                this._template = template;
            }

            public IEnumerable<SqlScript> GetScripts(IConnectionManager connectionManager)
            {
                foreach (var script in _inner.GetScripts(connectionManager))
                {
                    // TODO Log (Verbose: Full script)
                    yield return new SqlScript(
                        script.Name,
                        ConvertViewScript(script.Contents, Path.GetFileNameWithoutExtension(script.Name), _template),
                        script.SqlScriptOptions
                    );
                }
            }

            private static string ConvertViewScript(string content, string scriptName, string template) =>
                String.IsNullOrEmpty(template)
                ? content
                : new StringBuilder(template)
                    .Replace("%%SCRIPT_NAME%%", scriptName)
                    .Replace("%%SCRIPT_CONTENT%%", content)
                    .ToString();

        }

    }
}