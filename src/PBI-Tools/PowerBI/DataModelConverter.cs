// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json.Linq;
using PbiTools.Utils;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.PowerBI
{
    public class DataModelConverter : IPowerBIPartConverter<JObject>
    {
        private readonly string _modelName;
        private readonly IDependenciesResolver _resolver;

        public DataModelConverter(string modelName, IDependenciesResolver resolver)
        {
            _modelName = modelName;
            _resolver = resolver;
        }

        public JObject FromPackagePart(IStreamablePowerBIPackagePartContent part)
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
                msmdsrv.LoadPbixModel(part, _modelName, _modelName);

                using (var server = new TOM.Server())
                {
                    server.Connect(msmdsrv.ConnectionString);
                    using (var db = server.Databases[_modelName])
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
        }

        public IStreamablePowerBIPackagePartContent ToPackagePart(JObject content)
        {
            // TODO Create empty Database, deploy TMSL...
            throw new NotImplementedException();
        }
    }
}