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
    /// Reads the contents of PBIX files and converts each of their parts into generic (non-PowerBI) data formats (like JObject, XDocument, ZipArchive).
    /// Each instance handles one file only.
    /// </summary>
    public class PbixReader : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixReader>();

        private readonly Stream _pbixStream;
        private readonly IPowerBIPackage _package;
        private readonly PowerBIPartConverters _converters;



        public PbixReader(string pbixPath, IDependenciesResolver resolver)
        {
            if (pbixPath == null) throw new ArgumentNullException(nameof(pbixPath));
            if (!File.Exists(pbixPath)) throw new FileNotFoundException("PBIX file not found.", pbixPath);

            _pbixStream = File.OpenRead(pbixPath);
            _package = PowerBIPackager.Open(_pbixStream); // TODO Handle errors

            const string
                forbiddenCharacters =
                    @". , ; ' ` : / \ * | ? "" & % $ ! + = ( ) [ ] { } < >"; // grabbed these from an AMO exception message
            var modelName = forbiddenCharacters // TODO Could also use TOM.Server.Databases.CreateNewName()
                .Replace(" ", "")
                .ToCharArray()
                .Aggregate(
                    Path.GetFileNameWithoutExtension(pbixPath),
                    (n, c) => n.Replace(c, '_')
                );

            _converters = new PowerBIPartConverters(modelName, resolver);
        }

        public PbixReader(IPowerBIPackage powerBIPackage, IDependenciesResolver resolver)
        {
            _package = powerBIPackage ?? throw new ArgumentNullException(nameof(powerBIPackage)); // TODO Handle errors
            _converters = new PowerBIPartConverters(Guid.NewGuid().ToString(), resolver);
        }




        public JObject ReadConnections()
        {
            return _converters.Connections.FromPackagePart(_package.Connections);
        }

        public JObject ReadDataModel()
        {
            if (_package.DataModelSchema != null)
            {
                return _converters.DataModelSchema.FromPackagePart(_package.DataModelSchema);
            }
            else if (_package.DataModel != null)
            {
                return _converters.DataModel.FromPackagePart(_package.DataModel);
            }

            return default(JObject);
        }

        public MashupParts ReadMashup()
        {
            // Mashup is NOT optional
            return _converters.DataMashup.FromPackagePart(_package.DataMashup);
        }

        public JObject ReadReport()
        {
            return _converters.ReportDocument.FromPackagePart(_package.ReportDocument);
        }

        public JObject ReadDiagramViewState()
        {
            return _converters.DiagramViewState.FromPackagePart(_package.DiagramViewState);
        }

        public JObject ReadDiagramLayout()
        {
            return _converters.DiagramLayout.FromPackagePart(_package.DiagramLayout);
        }

        public XDocument ReadLinguisticSchema()
        {
            return _converters.LinguisticSchema.FromPackagePart(_package.LinguisticSchema);
        }

        public JObject ReadReportMetadata()
        {
            return _converters.ReportMetadata.FromPackagePart(_package.ReportMetadata);
        }

        public JObject ReadReportSettings()
        {
            return _converters.ReportSettings.FromPackagePart(_package.ReportSettings);
        }

        public string ReadVersion()
        {
            return _converters.Version.FromPackagePart(_package.Version);
        }

        #region Resources

        public IDictionary<string, byte[]> ReadCustomVisuals()
        {
            return ReadResources(_package.CustomVisuals);
        }

        public IDictionary<string, byte[]> ReadStaticResources()
        {
            return ReadResources(_package.StaticResources);
        }

        private IDictionary<string, byte[]> ReadResources(IDictionary<Uri, IStreamablePowerBIPackagePartContent> part)
        {
            return part?.Aggregate(
                new Dictionary<string, byte[]>(), 
                (result, entry) =>
                {
                    result.Add(entry.Key.ToString(), _converters.Resources.FromPackagePart(entry.Value));
                    return result;
                });
        }
        

        #endregion

        public void Dispose()
        {
            _package.Dispose();
            _pbixStream?.Dispose();  // null if initialized from existing package
        }

    }

    /*
       Converters translate between the binary IStreamablePowerBIPackagePartContent and a readable representation of the content.
       Serializers convert to and from the PbixProj format.
       ------------------------------------
       | PBIX                             | - File System
       | IPowerBIPackage                  | - Power BI API
       | PbixReader|Writer                | - pbi-tools
       | IPowerBIPartConverter            |   - IStreamablePowerBIPackagePartContent <==> JObject, XDocument, ZipArchive, String (RAW viewable content)
       | IPowerBIPartSerializer           |   - PbixProj File System (FORMATTED content)
       ------------------------------------
     */
}