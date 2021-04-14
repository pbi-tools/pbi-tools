// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerBI.Packaging;
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

    internal class PbiPackage : IPowerBIPackage
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

        public IStreamablePowerBIPackagePartContent Connections { 
            get => _converters.Connections.ToPackagePart(_pbixModel.Connections); 
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent DataMashup {
            get => new MashupConverter().ToPackagePart(_pbixModel.DataMashup);
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent DataModel {
            get => _format == PbiFileFormat.PBIX ? _converters.DataModel.ToPackagePart(_pbixModel.DataModel) : EmptyContent;
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent DataModelSchema {
            get => _format == PbiFileFormat.PBIT ? _converters.DataModelSchema.ToPackagePart(_pbixModel.DataModel) : EmptyContent;
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent DiagramViewState {
            get => _converters.DiagramViewState.ToPackagePart(_pbixModel.DiagramViewState); 
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent DiagramLayout {
            get => _converters.DiagramLayout.ToPackagePart(_pbixModel.DiagramLayout); 
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent ReportDocument {
            get => _converters.ReportDocument.ToPackagePart(_pbixModel.Report); 
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent LinguisticSchema {
            get => _pbixModel.LinguisticSchemaXml == null
                ? _converters.LinguisticSchemaV3.ToPackagePart(_pbixModel.LinguisticSchema)
                : _converters.LinguisticSchema.ToPackagePart(_pbixModel.LinguisticSchemaXml); 
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent ReportMetadata {
            get => _converters.ReportMetadataV3.ToPackagePart(_pbixModel.ReportMetadata); 
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent ReportSettings {
            get => _converters.ReportSettingsV3.ToPackagePart(_pbixModel.ReportSettings); 
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent Version {
            get => _converters.Version.ToPackagePart(_pbixModel.Version); 
            set => throw new NotSupportedException();
        }

        public IStreamablePowerBIPackagePartContent CustomProperties {
            get => EmptyContent; 
            set => throw new NotSupportedException();
        }

        public IDictionary<Uri, IStreamablePowerBIPackagePartContent> CustomVisuals {
            get => ConvertResources(_pbixModel.CustomVisuals); 
            set => throw new NotSupportedException();
        }

        public IDictionary<Uri, IStreamablePowerBIPackagePartContent> StaticResources {
            get => ConvertResources(_pbixModel.StaticResources); 
            set => throw new NotSupportedException();
        }

        private IDictionary<Uri, IStreamablePowerBIPackagePartContent> ConvertResources(IDictionary<string, byte[]> resources) =>
            resources != null
            ? resources.Aggregate(new Dictionary<Uri, IStreamablePowerBIPackagePartContent>(), (dict, entry) => {
                    dict.Add(new Uri(entry.Key, UriKind.Relative), _converters.Resources.ToPackagePart(entry.Value)); // TODO Can use alternative ctor with Func<Stream>
                    return dict;
                }) 
            : new Dictionary<Uri, IStreamablePowerBIPackagePartContent>();

        public void Dispose()
        {
        }
    }
}