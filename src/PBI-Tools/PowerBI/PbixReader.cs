// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json.Linq;
using PbiTools.Model;
using PbiTools.Utils;
using Serilog;

namespace PbiTools.PowerBI
{
    /// <summary>
    /// Reads the contents of PBIX files and converts each of their parts into generic (non-PowerBI)
    /// data formats (like JObject, XDocument, ZipArchive).
    /// Each instance handles one file only.
    /// </summary>
    public class PbixReader : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixReader>();

        private readonly Stream _pbixStream;
        private readonly IPowerBIPackage _package;
        private readonly PowerBIPartConverters _converters;
        private readonly Lazy<PowerBILegacyPartConverters> _legacyConverters
            /* Lazy instantiation ensures legacy types are only getting resolved if needed -- API incompatibilities are more likely to occur with legacy types */
            = new Lazy<PowerBILegacyPartConverters>(() => new PowerBILegacyPartConverters());



        public PbixReader(string pbixPath, IDependenciesResolver resolver)
        {
            if (pbixPath == null) throw new ArgumentNullException(nameof(pbixPath));
            if (!File.Exists(pbixPath)) throw new FileNotFoundException("PBIX file not found.", pbixPath);

            _pbixStream = new FileStream(pbixPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _package = PowerBIPackager.Open(_pbixStream); // TODO Handle errors
            this.Path = pbixPath;

            var modelName = PowerBIPartConverters.ConvertToValidModelName(System.IO.Path.GetFileNameWithoutExtension(pbixPath));
            _converters = new PowerBIPartConverters(modelName, resolver);
        }

        public PbixReader(IPowerBIPackage powerBIPackage, IDependenciesResolver resolver)
        {
            _package = powerBIPackage ?? throw new ArgumentNullException(nameof(powerBIPackage));
            _converters = new PowerBIPartConverters(Guid.NewGuid().ToString(), resolver);
        }


        public string Path { get; }


        public JObject ReadConnections()
        {
            return _converters.Connections.FromPackagePart(_package.Connections.AsFunc());
        }

        public JObject ReadDataModel()
        {
            if (_package.DataModelSchema != null)
            {
                Log.Information("Extracting DataModel from PBIT");
                return _converters.DataModelSchema.FromPackagePart(_package.DataModelSchema.AsFunc());
            }
            else if (_package.DataModel != null)
            {
                Log.Information("Extracting DataModel from PBIX");
                return _converters.DataModel.FromPackagePart(_package.DataModel.AsFunc());
            }

            return default(JObject);
        }

        public JObject ReadDataModelFromRunningInstance(int port)
        {
            Log.Information("Extracting DataModel from {Server}", $"localhost:{port}");
            return DataModelConverter.ExtractModelFromAS($".:{port}", dbs => dbs[0]);
        }

        public MashupParts ReadMashup()
        {
            if (_package.DataMashup != null)
            {
                return new MashupConverter().FromPackagePart(_package.DataMashup.AsFunc(), null);
            }
            return default(MashupParts);
        }

        public JObject ReadReport()
        {
            return _converters.ReportDocument.FromPackagePart(_package.ReportDocument.AsFunc());
        }

        public JObject ReadDiagramViewState()
        {
            return _converters.DiagramViewState.FromPackagePart(_package.DiagramViewState.AsFunc());
        }

        public JObject ReadDiagramLayout()
        {
            return _converters.DiagramLayout.FromPackagePart(_package.DiagramLayout.AsFunc());
        }

        public XDocument ReadLinguisticSchema()
        {
            return _converters.LinguisticSchema.FromPackagePart(_package.LinguisticSchema.AsFunc());
        }

        public JObject ReadLinguisticSchemaV3()
        {
            return _converters.LinguisticSchemaV3.FromPackagePart(_package.LinguisticSchema.AsFunc());
        }

        public JObject ReadReportMetadata()
        {
            return _legacyConverters.Value.ReportMetadata.FromPackagePart(_package.ReportMetadata.AsFunc());
        }

        public JObject ReadReportMetadataV3()
        {
            return _converters.ReportMetadataV3.FromPackagePart(_package.ReportMetadata.AsFunc());
        }

        public JObject ReadReportSettings()
        {
            return _legacyConverters.Value.ReportSettings.FromPackagePart(_package.ReportSettings.AsFunc());
        }

        public JObject ReadReportSettingsV3()
        {
            return _converters.ReportSettingsV3.FromPackagePart(_package.ReportSettings.AsFunc());
        }

        public string ReadVersion()
        {
            return _converters.Version.FromPackagePart(_package.Version.AsFunc());
        }

#region Resources

        public IDictionary<string, byte[]> ReadCustomVisuals()
        {
            return ReadResources(_package.CustomVisuals, _converters.CustomVisuals);
        }

        public IDictionary<string, byte[]> ReadStaticResources()
        {
            return ReadResources(_package.StaticResources, _converters.StaticResources);
        }

        private IDictionary<string, byte[]> ReadResources(IDictionary<Uri, IStreamablePowerBIPackagePartContent> part, IPowerBIPartConverter<byte[]> converter)
        {
            return part?.Aggregate(
                new Dictionary<string, byte[]>(),
                (result, entry) =>
                {
                    result.Add(entry.Key.ToString(), converter.FromPackagePart(entry.Value.GetStream));
                    return result;
                });
        }

#endregion



        public void Dispose()
        {
            Log.Debug("Closing PBIX file.");
            _package.Dispose();
            _pbixStream?.Dispose();  // null if initialized from existing package
        }

    }

    public static class PowerBIPackageExtensions
    {
        public static Func<Stream> AsFunc(this IStreamablePowerBIPackagePartContent partContent) =>
            partContent == null
                ? null
                : partContent.GetStream;
    }

    /*
       Converters translate between the binary IStreamablePowerBIPackagePartContent and a readable representation of the content.
       Serializers convert to and from the PbixProj format.
       ------------------------------------
       | *.pbix|pbit                      | - File System
       | IPowerBIPackage                  | - Power BI API
       | PbixReader|Writer                | - pbi-tools
       | IPowerBIPartConverter            |   - IStreamablePowerBIPackagePartContent <==> JObject, XDocument, ZipArchive, String (RAW viewable content)
       | IPowerBIPartSerializer           |   - PbixProj File System (FORMATTED content)
       ------------------------------------
     */
}
#endif