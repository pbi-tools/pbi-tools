// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Castle.DynamicProxy;
#if NETFRAMEWORK
using Microsoft.PowerBI.Packaging;
#endif
using PbiTools.Model;
using Serilog;

namespace PbiTools.PowerBI
{
    public enum PbiFileFormat
    {
        [PowerArgs.ArgDescription("Creates a file using the PBIX format. If the file contains a data model it will have no data and will require processing. This is the default format.")]
        PBIX = 1,
        [PowerArgs.ArgDescription("Creates a file using the PBIT format. When opened in Power BI Desktop, parameters and/or credentials need to be provided and a refresh is triggered.")]
        PBIT = 2
    }

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
            => _converters.Connections.ToPackagePart(_pbixModel.Connections); 

        public IStreamablePowerBIPackagePartContent DataMashup
            => new MashupConverter().ToPackagePart(_pbixModel.DataMashup);

        public IStreamablePowerBIPackagePartContent DataModel
            => _format == PbiFileFormat.PBIX ? _converters.DataModel.ToPackagePart(_pbixModel.DataModel) : EmptyContent;

        public IStreamablePowerBIPackagePartContent DataModelSchema
            => _format == PbiFileFormat.PBIT ? _converters.DataModelSchema.ToPackagePart(_pbixModel.DataModel) : EmptyContent;

        public IStreamablePowerBIPackagePartContent DiagramViewState
            => _converters.DiagramViewState.ToPackagePart(_pbixModel.DiagramViewState); 

        public IStreamablePowerBIPackagePartContent DiagramLayout
            => _converters.DiagramLayout.ToPackagePart(_pbixModel.DiagramLayout); 

        public IStreamablePowerBIPackagePartContent ReportDocument
            => _converters.ReportDocument.ToPackagePart(_pbixModel.Report); 

        public IStreamablePowerBIPackagePartContent LinguisticSchema
            => _pbixModel.LinguisticSchemaXml == null
                ? _converters.LinguisticSchemaV3.ToPackagePart(_pbixModel.LinguisticSchema)
                : _converters.LinguisticSchema.ToPackagePart(_pbixModel.LinguisticSchemaXml); 

        public IStreamablePowerBIPackagePartContent ReportMetadata
            => _converters.ReportMetadataV3.ToPackagePart(_pbixModel.ReportMetadata); 

        public IStreamablePowerBIPackagePartContent ReportSettings
            => _converters.ReportSettingsV3.ToPackagePart(_pbixModel.ReportSettings); 

        public IStreamablePowerBIPackagePartContent Version
            => _converters.Version.ToPackagePart(_pbixModel.Version); 

        public IDictionary<Uri, IStreamablePowerBIPackagePartContent> CustomVisuals
            => ConvertResources(_pbixModel.CustomVisuals); 

        public IDictionary<Uri, IStreamablePowerBIPackagePartContent> StaticResources
            => ConvertResources(_pbixModel.StaticResources); 

        private IDictionary<Uri, IStreamablePowerBIPackagePartContent> ConvertResources(IDictionary<string, byte[]> resources) =>
            resources?.Aggregate(new Dictionary<Uri, IStreamablePowerBIPackagePartContent>(), 
                (dict, entry) => {
                    dict.Add(
                        new Uri(entry.Key, UriKind.Relative), 
                        _converters.Resources.ToPackagePart(entry.Value)); // TODO Could use alternative ctor with Func<Stream>
                    return dict;
                }) 
            ?? new Dictionary<Uri, IStreamablePowerBIPackagePartContent>();


        public void Save(string path)
        {
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
}