using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using WildcardMatch;
using AMO = Microsoft.AnalysisServices;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.Deployments
{
    using Configuration;

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

        private void RequestModelRefresh(TOM.RefreshType refreshType)
        {
            if (ManifestOptions.IgnoreRefreshPolicy)
            {
                Log.Information("Ignoring RefreshPolicy.");
                _database.Model.RequestRefresh(refreshType, TOM.RefreshPolicyBehavior.Ignore);
            }
            if (TryGetEffectiveDate(ManifestOptions, out var effectiveDate))
            {
                Log.Information("Using effective date: {EffectiveDate}", effectiveDate);
                _database.Model.RequestRefresh(refreshType, effectiveDate);
            }
            else
            {
                _database.Model.RequestRefresh(refreshType);
            }
        }

        private void RequestTableRefresh(string tableName, TOM.RefreshType refreshType)
        {
            if (ManifestOptions.IgnoreRefreshPolicy)
            {
                Log.Information("Ignoring RefreshPolicy.");
                _database.Model.Tables[tableName].RequestRefresh(refreshType, TOM.RefreshPolicyBehavior.Ignore);
            }
            if (TryGetEffectiveDate(ManifestOptions, out var effectiveDate))
            {
                Log.Information("Using effective date: {EffectiveDate}", effectiveDate);
                _database.Model.Tables[tableName].RequestRefresh(refreshType, effectiveDate);
            }
            else
            {
                _database.Model.Tables[tableName].RequestRefresh(refreshType);
            }
        }

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
                        RequestModelRefresh(refreshable.RefreshType);
                        break;
                    case RefreshObjectType.Table:
                        Log.Information("Refreshing table {Table} ({RefreshType})...", refreshable.Table, refreshable.RefreshType);
                        RequestTableRefresh(refreshable.Table, refreshable.RefreshType);
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
                .Select(p => new ModelPartition(p.Table.Name, p.Name, p.SourceType))
                .ToArray();

        public static ModelRefreshable[] CalculateRefreshables(ModelPartition[] partitions,
            PbiDeploymentOptions.RefreshOptions manifestOptions,
            PbiDeploymentEnvironment.RefreshOptions environmentOptions)
        {
            var hasEnvObjects = environmentOptions != null
                && !environmentOptions.Skip
                && environmentOptions.Objects != null
                && !environmentOptions.Objects.IsEmpty;

            var defaultRefreshType = (environmentOptions?.Type ?? manifestOptions.Type).ConvertToTOM();
            Log.Information("Using default refresh type: {RefreshType}", defaultRefreshType);

            if ((manifestOptions.Objects == null || manifestOptions.Objects.IsEmpty)
                && !hasEnvObjects)
            {
                Log.Debug("No refresh objects specified. Entire model will be refreshed.");
                return new[] { new ModelRefreshable(RefreshObjectType.Model, defaultRefreshType) };
            }

            var allPartitions = new List<ModelPartition>(partitions);
            var results = new List<ModelRefreshable>();

            if (hasEnvObjects)
                Log.Debug("Processing {ExpressionCount} refresh object expressions from current environment.", environmentOptions.Objects.Count());
            else
                Log.Debug("Processing {ExpressionCount} refresh object expressions from manifest.", manifestOptions.Objects.Count());

            foreach (var refreshObj in (hasEnvObjects ? environmentOptions.Objects : manifestOptions.Objects))
            {
                Log.Debug("Applying refresh object expression: {Expression}", refreshObj.OriginalString);

                if (refreshObj.ObjectType == RefreshObjectType.Table)
                {
                    // Identify remaining tables matching current expression
                    var selectedTables = allPartitions
                        .GroupBy(p => p.Table)
                        .Where(g => refreshObj.TableExpression.WildcardMatch(g.Key))
                        .Select(g => g.Key)
                        .ToArray();

                    // Add tables to refreshables, unless 'None' selected
                    if (refreshObj.RefreshType.HasValue)
                        Array.ForEach(selectedTables, t =>
                        {
                            Log.Debug("Requesting {RefreshType} for table {Table}.", refreshObj.RefreshType.Value, t);
                            results.Add(new ModelRefreshable(RefreshObjectType.Table, refreshObj.RefreshType.Value.ConvertToTOM(), t));
                        });

                    // Remove all partitions belonging to matching tables from initial list
                    allPartitions.RemoveAll(p => refreshObj.TableExpression.WildcardMatch(p.Table));
                }
                else if (refreshObj.ObjectType == RefreshObjectType.Partition)
                {
                    // Identify remaining tables matching current expression
                    var selectedPartitions = allPartitions
                        .Where(p => refreshObj.OriginalString.WildcardMatch(p.ToString()))
                        .ToArray();

                    // Add partitions to refreshables, unless 'None' selected
                    if (refreshObj.RefreshType.HasValue)
                        Array.ForEach(selectedPartitions, p =>
                        {
                            Log.Debug("Requesting {RefreshType} for partition {Table}|{Partition}.", refreshObj.RefreshType.Value, p.Table, p.Partition); 
                            results.Add(new ModelRefreshable(RefreshObjectType.Partition, refreshObj.RefreshType.Value.ConvertToTOM(), p.Table, p.Partition));
                        });

                    // Remove all selected partitions from initial list
                    Array.ForEach(selectedPartitions, p => allPartitions.Remove(p));
                }
            }

            // Handle remaining partitions (using manifest type)
            foreach (var p in allPartitions)
            {
                if (manifestOptions.SkipRefreshPolicyPartitions && p.SourceType == TOM.PartitionSourceType.PolicyRange)
                    continue;

                Log.Verbose("Requesting default refresh type '{RefreshType}' for partition: {Partition}", defaultRefreshType, p);
                results.Add(new ModelRefreshable(RefreshObjectType.Partition,
                    defaultRefreshType, 
                    p.Table, 
                    p.Partition)
                );
            }

            return results.ToArray();            
        }

        internal static bool TryGetEffectiveDateFromEnv(out DateTime effectiveDate)
        {
            var envValue = AppSettings.GetEnvironmentSetting(Env.EffectiveDate);
            if (envValue == null)
            {
                effectiveDate = default;
                return false;
            }

            return DateTime.TryParse(envValue,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out effectiveDate);
        }

        internal static bool TryGetEffectiveDate(PbiDeploymentOptions.RefreshOptions refreshOptions, out DateTime effectiveDate)
        {
            if (TryGetEffectiveDateFromEnv(out effectiveDate))
                return true;

            if (!refreshOptions.EffectiveDate.HasValue) { 
                effectiveDate = default;
                return false;
            }

            effectiveDate = refreshOptions.EffectiveDate.Value;
            return true;
        }

    }

    public struct ModelPartition
    {
        public ModelPartition(string table, string partition, TOM.PartitionSourceType sourceType)
        {
            Table = table ?? throw new ArgumentNullException(nameof(table));
            Partition = partition ?? throw new ArgumentNullException(nameof(partition));
            SourceType = sourceType;
        }

        public string Table { get; }
        public string Partition { get; }
        public TOM.PartitionSourceType SourceType { get; }

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