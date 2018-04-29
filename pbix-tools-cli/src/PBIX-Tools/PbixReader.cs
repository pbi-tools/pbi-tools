using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Mashup.Client.Packaging;
using Microsoft.Mashup.Client.Packaging.BinarySerialization;
using Microsoft.Mashup.Client.Packaging.SerializationObjectModel;
using Microsoft.Mashup.Client.Packaging.Serializers;
using Microsoft.Mashup.Host.Document.Storage;
using Microsoft.PowerBI.Client.Windows;
using Microsoft.PowerBI.Packaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace PbixTools
{
    public class PbixReader : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixReader>();

        private readonly string _pbixPath;
        private readonly Stream _pbixStream;
        private readonly IPowerBIPackage _package;
        private readonly IDependenciesResolver _resolver;

        private readonly JsonSerializer _serializer = new JsonSerializer();


        public PbixReader(string pbixPath, IDependenciesResolver resolver)
        {
            _pbixPath = pbixPath ?? throw new ArgumentNullException(nameof(pbixPath));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            if (!File.Exists(pbixPath)) throw new FileNotFoundException("PBIX file not found.", pbixPath);

            _pbixStream = File.OpenRead(pbixPath);
            _package = PowerBIPackager.Open(_pbixStream); // TODO Handle errors
        }

        public JObject ReadConnections()
        {
            if (_package.Connections == null) return default(JObject);
            using (var reader = new JsonTextReader(new StreamReader(_package.Connections.GetStream())))
            {
                return _serializer.Deserialize<JObject>(reader);
            }
        }

        public JObject ReadDataModel()
        {
            var tmsl = default(JObject);

            if (_package.DataModelSchema != null)
            {
                using (var reader = new JsonTextReader(new StreamReader(_package.DataModelSchema.GetStream(), Encoding.Unicode)))
                {
                    tmsl = new JsonSerializer().Deserialize<JObject>(reader);
                }
            }
            else if (_package.DataModel != null)
            {
                const string
                    forbiddenCharacters =
                        @". , ; ' ` : / \ * | ? "" & % $ ! + = ( ) [ ] { } < >"; // grabbed these from an AMO exception message
                var modelName = forbiddenCharacters // TODO Could also use TOM.Server.Databases.CreateNewName()
                    .Replace(" ", "")
                    .ToCharArray()
                    .Aggregate(
                        Path.GetFileNameWithoutExtension(_pbixPath),
                        (n, c) => n.Replace(c, '_')
                    );

                using (var msmdsrv = new AnalysisServicesServer(new ASInstanceConfig
                {
                    DeploymentMode = DeploymentMode.SharePoint, // required for PBI Desktop
                    DisklessModeRequested = true,
                    EnableDisklessTMImageSave = true,
                    // Dirs will be set automatically
                }, _resolver))
                {
                    msmdsrv.HideWindow = true;

                    msmdsrv.Start();
                    msmdsrv.LoadPbixModel(_package, modelName, modelName);

                    using (var server = new Microsoft.AnalysisServices.Tabular.Server())
                    {
                        server.Connect(msmdsrv.ConnectionString);
                        using (var db = server.Databases[modelName])
                        {
                            var json = Microsoft.AnalysisServices.Tabular.JsonSerializer.SerializeDatabase(db, new Microsoft.AnalysisServices.Tabular.SerializeOptions
                            {
                                IgnoreTimestamps = true, // that way we don't have to strip them out below
                                IgnoreInferredObjects = true,
                                IgnoreInferredProperties = true,
                                SplitMultilineStrings = true
                            });
                            tmsl = JObject.Parse(json);
                        }
                    }
                }
            }

            return tmsl;
        }

        private PackageComponents _packageComponents;
        private static readonly XmlSerializer PackageMetadataXmlSerializer = new XmlSerializer(typeof(SerializedPackageMetadata));

        private void EnsurePackageComponents()
        {
            if (_packageComponents != null) return;
            if (!(new PowerBIDesktopMashupReader()).TryGetMashupBytes(_pbixStream, out var numArray) || !PackageComponents.TryDeserialize(numArray, out _packageComponents))
            {
                throw new Exception("Could not read MashupPackage"); // TODO Better error handling
            }
        }

        public Package ReadMashupPackage()
        {
            EnsurePackageComponents();

            return Package.Open(new MemoryStream(_packageComponents.PartsBytes));
        }

        public JObject ReadMashupPermissions()
        {
            EnsurePackageComponents();

            var permissions = PermissionsSerializer.Deserialize(_packageComponents.PermissionBytes);
            return JObject.FromObject(permissions);
        }

        public XDocument ReadMashupMetadata()
        {
            EnsurePackageComponents();

            if (PackageMetadataSerializer.TryDeserialize(_packageComponents.MetadataBytes, out SerializedPackageMetadata packageMetadata, out byte[] contentStorageBytes))
            {
                using (var stringWriter = new StringWriter(CultureInfo.InvariantCulture))
                {
                    using (var writer = XmlWriter.Create(stringWriter, new XmlWriterSettings()))
                    {
                        PackageMetadataXmlSerializer.Serialize(writer, packageMetadata);
                    }

                    return XDocument.Parse(stringWriter.ToString());
                }
            }

            return default(XDocument);
        }

        public ZipArchive ReadMashupContent()
        {
            EnsurePackageComponents();

            if (PackageMetadataSerializer.TryDeserialize(_packageComponents.MetadataBytes, out SerializedPackageMetadata packageMetadata, out byte[] contentStorageBytes))
            {
                return new ZipArchive(new MemoryStream(contentStorageBytes), ZipArchiveMode.Read, leaveOpen: false);
            }

            return default(ZipArchive);
        }

        public JObject ReadReport()
        {
            using (var reader = new JsonTextReader(new StreamReader(_package.ReportDocument.GetStream(), Encoding.Unicode /* this is the crucial bit! */)))
            {
                return _serializer.Deserialize<JObject>(reader);
            }
        }

        public JObject ReadDiagramViewState()
        {
            using (var reader = new JsonTextReader(new StreamReader(_package.DiagramViewState.GetStream(), Encoding.Unicode)))
            {
                return _serializer.Deserialize<JObject>(reader);
            }
        }

        public XDocument ReadLinguisticSchema()
        {
            if (_package.LinguisticSchema == null) return default(XDocument);
            using (var reader = new StreamReader(_package.LinguisticSchema.GetStream(), Encoding.Unicode))
            {
                return XDocument.Load(reader);
            }
        }

        public JObject ReadReportMetadata()
        {
            if (_package.ReportMetadata == null) return default(JObject);
            using (var reader = new BinarySerializationReader(_package.ReportMetadata.GetStream()))
            {
                var metadata = new ReportMetadata();
                metadata.Deserialize(reader);

                return JObject.FromObject(metadata);
            }
        }

        public JObject ReadReportSettings()
        {
            if (_package.ReportSettings == null) return default(JObject);
            using (var reader = new BinarySerializationReader(_package.ReportSettings.GetStream()))
            {
                var settings = new ReportSettings();
                settings.Deserialize(reader);

                return JObject.FromObject(settings);
            }
        }

        public string ReadVersion()
        {
            using (var reader = new StreamReader(_package.Version.GetStream(), Encoding.Unicode))
            {
                return reader.ReadToEnd();
            }
        }

        public IDictionary<string, byte[]> ReadCustomVisuals()
        {
            return ReadResources(_package.CustomVisuals);
        }

        public IDictionary<string, byte[]> ReadStaticResources()
        {
            return ReadResources(_package.StaticResources);
        }

        private static IDictionary<string, byte[]> ReadResources(IDictionary<Uri, IStreamablePowerBIPackagePartContent> part)
        {
            return part?.Aggregate(
                new Dictionary<string, byte[]>(), 
                (result, entry) =>
                {
                    result.Add(entry.Key.ToString(), PowerBIPackagingUtils.GetContentAsBytes(entry.Value, isOptional: true));
                    return result;
                });
        }


        public void Dispose()
        {
            _package.Dispose();
            _pbixStream.Dispose();
        }

    }
}