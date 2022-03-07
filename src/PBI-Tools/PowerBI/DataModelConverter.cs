// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using AMO = Microsoft.AnalysisServices;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.PowerBI
{
    using Utils;

    public class DataModelConverter : IPowerBIPartConverter<JObject>
    {
        private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<DataModelConverter>();

        private readonly string _modelName;
        private readonly IDependenciesResolver _resolver;

        public Uri PartUri { get; }

        public bool IsOptional { get; set; } = true;
        public string ContentType { get; set; } = PowerBIPartConverters.ContentTypes.DEFAULT;

        public DataModelConverter(string relativePartUri, string modelName, IDependenciesResolver resolver) : this(new Uri(relativePartUri, UriKind.Relative), modelName, resolver)
        { }

        public DataModelConverter(Uri partUri, string modelName, IDependenciesResolver resolver)
        {
            this.PartUri = partUri;
            _modelName = modelName;
            _resolver = resolver;
        }

        public JObject FromPackagePart(Func<Stream> part, string contentType)
        {
            if (!part.TryGetStream(out var stream)) return default(JObject);
            return LaunchTabularServerAndExecuteCallback(msmdsrv => 
            {
                msmdsrv.LoadPbixModel(stream, _modelName, _modelName);
                return ExtractModelFromAS(msmdsrv.ConnectionString, databases => databases[_modelName]);
            });
        }

        internal static JObject ExtractModelFromAS(string connectionString, Func<TOM.DatabaseCollection, TOM.Database> selectDb)
        { 
            using (var server = new TOM.Server())
            {
                server.Connect(connectionString);

                if (server.Databases.Count == 0)
                    return default;

                using (var db = selectDb(server.Databases))
                {
                    var json = TOM.JsonSerializer.SerializeDatabase(db, new TOM.SerializeOptions
                    {
                        IgnoreTimestamps = true, // that way we don't have to strip them out later
                        IgnoreInferredObjects = true,
                        IgnoreInferredProperties = true,
                        SplitMultilineStrings = true
                    });
                    return JObject.Parse(json);
                }
            }
        }

        private T LaunchTabularServerAndExecuteCallback<T>(Func<AnalysisServicesServer, T> callback)
        { 
            using (var msmdsrv = new AnalysisServicesServer(new ASInstanceConfig
            {
                DeploymentMode = DeploymentMode.SharePoint, // required for PBI Desktop
                // EnableMEngineIntegration = true,
                DisklessModeRequested = true,
                EnableDisklessTMImageSave = true,
                // Language = 1033,
                // Dirs will be set automatically
            }, _resolver))
            {
                msmdsrv.HideWindow = true;

                msmdsrv.Start();
                return callback(msmdsrv);
            }            
        }

        public Func<Stream> ToPackagePart(JObject content)
        {
            if (content == null) return null;

            return () => LaunchTabularServerAndExecuteCallback(msmdsrv => 
            {
                using (var server = new TOM.Server())
                {
                    server.Connect(msmdsrv.ConnectionString);

                    Log.Debug("Successfully connected to local MSMDSRV instance. ConnectionString: {ConnectionString}, DefaultCompatibilityLevel: {DefaultCompatibilityLevel}"
                        , msmdsrv.ConnectionString
                        , server.DefaultCompatibilityLevel);

                    using (var db = TOM.JsonSerializer.DeserializeDatabase(content.ToString()))
                    {
                        var stream = new MemoryStream();

                        server.Databases.Add(db);
                        db.Update(AMO.UpdateOptions.ExpandFull);

                        server.ImageSave(db.ID, stream);
                        return stream;
                    }
                }
            });
        }

    }
}
#endif