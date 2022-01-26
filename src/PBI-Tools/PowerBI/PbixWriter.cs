// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
#if NET
using System.IO.Packaging;
#endif
using System.Linq;
#if NETFRAMEWORK
using Castle.DynamicProxy;
using Microsoft.PowerBI.Packaging;
#endif
using Serilog;

namespace PbiTools.PowerBI
{
    using Model;

#if NETFRAMEWORK

    internal class PbiPackage
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbiPackage>();
	    internal static readonly IStreamablePowerBIPackagePartContent EmptyContent = new StreamablePowerBIPackagePartContent(default(string));

        private readonly PbixModel _pbixModel;
        private readonly PowerBIPartConverters _converters;
        private readonly PbiFileFormat _format;

        public PbiPackage(PbixModel pbixModel, PowerBIPartConverters converters, PbiFileFormat format = PbiFileFormat.PBIX)
        {
            this._pbixModel = pbixModel ?? throw new ArgumentNullException("pbixModel");
            this._converters = converters ?? throw new ArgumentNullException("converters");
            this._format = format;
        }

        public IStreamablePowerBIPackagePartContent Connections
            => ConvertContent(_converters.Connections, _pbixModel.Connections); 

        public IStreamablePowerBIPackagePartContent DataMashup
            => ConvertContent(new MashupConverter(), _pbixModel.DataMashup);

        public IStreamablePowerBIPackagePartContent DataModel
            => _format == PbiFileFormat.PBIX ? ConvertContent(_converters.DataModel, _pbixModel.DataModel) : EmptyContent;

        public IStreamablePowerBIPackagePartContent DataModelSchema
            => _format == PbiFileFormat.PBIT ? ConvertContent(_converters.DataModelSchema, _pbixModel.DataModel) : EmptyContent;

        public IStreamablePowerBIPackagePartContent DiagramViewState
            => ConvertContent(_converters.DiagramViewState, _pbixModel.DiagramViewState); 

        public IStreamablePowerBIPackagePartContent DiagramLayout
            => ConvertContent(_converters.DiagramLayout, _pbixModel.DiagramLayout); 

        public IStreamablePowerBIPackagePartContent ReportDocument
            => ConvertContent(_converters.ReportDocument, _pbixModel.Report); 

        public IStreamablePowerBIPackagePartContent LinguisticSchema
            => _pbixModel.LinguisticSchemaXml == null
                ? ConvertContent(_converters.LinguisticSchemaV3, _pbixModel.LinguisticSchema)
                : ConvertContent(_converters.LinguisticSchema, _pbixModel.LinguisticSchemaXml); 

        public IStreamablePowerBIPackagePartContent ReportMetadata
            => ConvertContent(_converters.ReportMetadataV3, _pbixModel.ReportMetadata); 

        public IStreamablePowerBIPackagePartContent ReportSettings
            => ConvertContent(_converters.ReportSettingsV3, _pbixModel.ReportSettings); 

        public IStreamablePowerBIPackagePartContent Version
            => ConvertContent(_converters.Version, _pbixModel.Version); 

        public IDictionary<Uri, IStreamablePowerBIPackagePartContent> CustomVisuals
            => ConvertResources(_pbixModel.CustomVisuals); 

        public IDictionary<Uri, IStreamablePowerBIPackagePartContent> StaticResources
            => ConvertResources(_pbixModel.StaticResources); 

        private IDictionary<Uri, IStreamablePowerBIPackagePartContent> ConvertResources(IDictionary<string, byte[]> resources) =>
            resources?.Aggregate(new Dictionary<Uri, IStreamablePowerBIPackagePartContent>(), 
                (dict, entry) => {
                    dict.Add(
                        new Uri(entry.Key, UriKind.Relative), 
                        new StreamablePowerBIPackagePartContent(entry.Value)
                    ); // TODO Could use alternative ctor with Func<Stream>
                    return dict;
                }) 
            ?? new Dictionary<Uri, IStreamablePowerBIPackagePartContent>();

        private static IStreamablePowerBIPackagePartContent ConvertContent<T>(IPowerBIPartConverter<T> converter, T value)
            where T : class
        {
            if (value == default(T)) return EmptyContent; // TODO Verify part is optional?
            return new StreamablePowerBIPackagePartContent(converter.ToPackagePart(value), converter.ContentType);
        }


        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (var pbixFile = File.Create(path))
            {
                var proxy = new ProxyGenerator();
                var powerbiPackage = proxy.CreateInterfaceProxyWithoutTarget<IPowerBIPackage>(new PowerBIPackageInterceptor(this));

                // TODO
                // - Generate empty Report part
                // - Generate Mashup part if pbix has DataModel

                Microsoft.PowerBI.Packaging.PowerBIPackager.Save(powerbiPackage, pbixFile);
            }
        }


        private class PowerBIPackageInterceptor : IInterceptor
        {
            private static readonly ILogger Log = Serilog.Log.ForContext<PowerBIPackageInterceptor>();
            private readonly PbiPackage _package;
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _propertyCache
                = new System.Collections.Concurrent.ConcurrentDictionary<string, object>();

            internal PowerBIPackageInterceptor(PbiPackage package)
            {
                this._package = package ?? throw new NullReferenceException("package");
            }

            void IInterceptor.Intercept(IInvocation invocation)
            {
                if (!invocation.Method.Name.StartsWith("get_"))
                    return;

                var propertyName = invocation.Method.Name.Substring("get_".Length);
                Log.Debug("PowerBIPackage - Intercepting: {PropertyName}", propertyName);

                var t = typeof(PbiPackage);
                var property = t.GetProperty(propertyName);

                Log.Debug("ReturnValue empty: {Empty}", invocation.ReturnValue == null);

                if (property != null)
                {
                    invocation.ReturnValue = _propertyCache.GetOrAdd(propertyName, _ => property.GetValue(_package));
                    Log.Debug("PowerBIPackage - Assigned value for: {PropertyName} from PbiPackage", propertyName);
                }
                else if (invocation.Method.ReturnType == typeof(IStreamablePowerBIPackagePartContent))
                {
                    invocation.ReturnValue = PbiPackage.EmptyContent;
                    Log.Debug("PowerBIPackage - Assigned empty content for: {PropertyName}", propertyName);
                }
            }
        }
    }

#endif



    public class PbixWriter
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<PbixWriter>();

        private readonly PbixModel _pbixModel;
        private readonly PowerBIPartConverters _converters;
        private readonly PbiFileFormat _format;

        public PbixWriter(PbixModel pbixModel, PowerBIPartConverters converters, PbiFileFormat format = PbiFileFormat.PBIX)
        {
            this._pbixModel = pbixModel ?? throw new ArgumentNullException("pbixModel");
            this._converters = converters ?? throw new ArgumentNullException("converters");
            this._format = format;
        }

#if NETFRAMEWORK
        internal static readonly IStreamablePowerBIPackagePartContent EmptyContent = new StreamablePowerBIPackagePartContent(default(string));

        public void WriteTo(string path)
        {
            throw new NotImplementedException();
        }
#endif

#if NET
        public void WriteTo(string path)
        {
            if (_format == PbiFileFormat.PBIX && _pbixModel.DataModel != null)
                throw new NotSupportedException("The pbi-tools Core version does not support compiling a PBIX with an embedded data model. Target a PBIT output instead.");

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using (var package = Package.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite)) // TODO Check overwrite?
            {
                var existingParts = package.GetParts().Select(p => p.Uri).ToList();
                var partsWritten = new List<Uri>();

                // Version
                ProcessPart(package, _converters.Version, _pbixModel.Version, partsWritten);
                // Settings
                ProcessPart(package, _converters.ReportSettingsV3, _pbixModel.ReportSettings, partsWritten);
                // Metadata
                ProcessPart(package, _converters.ReportMetadataV3, _pbixModel.ReportMetadata, partsWritten);
                // LinguisticSchema
                if (_pbixModel.LinguisticSchema != null)
                    ProcessPart(package, _converters.LinguisticSchemaV3, _pbixModel.LinguisticSchema, partsWritten);
                else
                    ProcessPart(package, _converters.LinguisticSchema, _pbixModel.LinguisticSchemaXml, partsWritten);
                // DiagramViewState
                ProcessPart(package, _converters.DiagramViewState, _pbixModel.DiagramViewState, partsWritten);
                // DiagramLayout
                ProcessPart(package, _converters.DiagramLayout, _pbixModel.DiagramLayout, partsWritten);
                // Report
                ProcessPart(package, _converters.ReportDocument, _pbixModel.Report, partsWritten);
                // DataModelSchema
                ProcessPart(package, _converters.DataModelSchema, _pbixModel.DataModel, partsWritten);
                // Connection
                ProcessPart(package, _converters.Connections, _pbixModel.Connections, partsWritten);

                // CustomVisuals
                foreach (var item in _pbixModel.CustomVisuals ?? new Dictionary<string, byte[]>())
                {
                    ProcessPart(package, _converters.CustomVisuals, item.Value, partsWritten, item.Key);
                }
                // StaticResources
                foreach (var item in _pbixModel.StaticResources ?? new Dictionary<string, byte[]>())
                {
                    ProcessPart(package, _converters.StaticResources, item.Value, partsWritten, item.Key);
                }

                // Remove obsolete parts
                foreach (var partToRemove in existingParts.Except(partsWritten).ToArray())
                {
                    Log.Debug("Removing obsolete part: {Uri}", partToRemove);
                    if (package.PartExists(partToRemove))
                        package.DeletePart(partToRemove);
                }
            }
        }

        internal static void ProcessPart<T>(Package package, IPowerBIPartConverter<T> converter, T content, IList<Uri> partsWritten, string subPartPath = null)
            where T : class
        {
            var uri = subPartPath == null
                ? converter.PartUri
                : new Uri($"{converter.PartUri.OriginalString}/{subPartPath}", UriKind.Relative);
            var part = EnsurePart(package, uri, converter.ContentType);

            var contentStream = converter.ToPackagePart(content);
            if (contentStream == null) 
            {
                package.DeletePart(uri);
                Log.Debug("Skipping empty package part {Uri}.", uri);
            }
            else
            {
                using (var partStream = part.GetStream(FileMode.Create))
                using (var sourceStream = contentStream())
                {
                    sourceStream.CopyTo(partStream);
                }

                Log.Information("Package part written: {Uri}", uri);
                partsWritten.Add(uri);
            }
        }

        private static PackagePart EnsurePart(Package package, Uri partUri, string contentType)
        {
            if (package.PartExists(partUri))
            {
                var part = package.GetPart(partUri);
                if (part.ContentType == contentType)
                    return part;
                package.DeletePart(partUri);
            }
            return package.CreatePart(partUri, contentType);
        }
#endif
    }
}