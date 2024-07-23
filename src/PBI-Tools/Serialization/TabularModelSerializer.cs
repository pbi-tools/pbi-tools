/*
 * This file is part of the pbi-tools project <https://github.com/pbi-tools/pbi-tools>.
 * Copyright (C) 2018 Mathias Thierbach
 *
 * pbi-tools is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * pbi-tools is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * A copy of the GNU Affero General Public License is available in the LICENSE file,
 * and at <https://goto.pbi.tools/license>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Serilog;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular.Extensions;
using Microsoft.AnalysisServices.Tabular.Serialization;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.Serialization
{
    using FileSystem;
    using ProjectSystem;

    /// <summary>
    /// Serializes a tabular database (represented as TMSL/json) into a <see cref="IProjectFolder"/>.
    /// </summary>
    public partial class TabularModelSerializer : IPowerBIPartSerializer<JObject>
    {
        internal static readonly ILogger Log = Serilog.Log.ForContext<TabularModelSerializer>();

        public const string FolderName = "Model";
        public const string DefaultDatabaseFileName = "database.json";

        private readonly IProjectFolder _modelFolder;
        private readonly ModelSettings _settings;
        private readonly string _fileName = DefaultDatabaseFileName;


        public TabularModelSerializer(IProjectFolder modelFolder, ModelSettings settings, string dbFileName = DefaultDatabaseFileName)
        {
            _modelFolder = modelFolder ?? throw new ArgumentNullException(nameof(modelFolder));
            _settings = settings;
            _fileName = dbFileName;
            _queries = new Dictionary<string, string>();
        }

        public string BasePath => _modelFolder.BasePath;

        #region Model Serialization

        public bool Serialize(JObject db)
        {
            if (db == null) return false;

            Log.Information("Using tabular model serialization mode: {Mode}", _settings.SerializationMode);

            switch (_settings.SerializationMode)
            {
                case ModelSerializationMode.Legacy:
                case ModelSerializationMode.Raw:
                    return SerializeLegacy(db);

                case ModelSerializationMode.Default:
                case ModelSerializationMode.Tmdl:
                    return SerializeTmdl(db);

                default:
                    throw new PbiToolsCliException(ExitCode.NotImplemented, $"ModelSerializationMode not implemented: {_settings.SerializationMode}.");
            }
        }

        internal bool SerializeTmdl(JObject db)
        {
            db = db
                .RemoveProperties(_settings?.IgnoreProperties)
                .ApplyAnnotationRules(_settings?.Annotations);

            var tomDb = TOM.JsonSerializer.DeserializeDatabase(db.ToString(),
                new TOM.DeserializeOptions { },
                CompatibilityMode.PowerBI);

            var modelFolder = new DirectoryInfo(_modelFolder.BasePath);
            if (modelFolder.Exists)
                modelFolder.Delete(recursive: true);

            var optionsBuilder = new MetadataSerializationOptionsBuilder(MetadataSerializationStyle.Tmdl);

            optionsBuilder = _settings?.ExcludeChildrenMetadata switch
            {
                true => optionsBuilder.WithoutChildrenMetadata(),
                false => optionsBuilder.WithChildrenMetadata(),
                _ => optionsBuilder
            };

            optionsBuilder = _settings?.IncludeRestrictedInformation switch
            {
                true => optionsBuilder.WithRestrictedInformation(),
                false => optionsBuilder.WithoutRestrictedInformation(),
                _ => optionsBuilder
            };

            optionsBuilder = _settings?.MetadataOrderHints switch
            {
                true => optionsBuilder.WithMetadataOrderHints(),
                false => optionsBuilder.WithoutMetadataOrderHints(),
                _ => optionsBuilder
            };

            optionsBuilder = _settings?.ExpressionTrimStyle switch
            {
                { } trimStyle => optionsBuilder.WithExpressionTrimStyle(trimStyle),
                _ => optionsBuilder
            };

            var formattingOptions = new MetadataFormattingOptionsBuilder();
            if (_settings?.Formatting is {} formatting)
            {
                if (!string.IsNullOrEmpty(formatting.Encoding))
                    formattingOptions =
                        formattingOptions.WithEncoding(formatting.GetEncoding());

                formattingOptions = formattingOptions
                    .WithNewLineStyle(formatting.NewLineStyle);

                formattingOptions = formatting.IndentationMode switch
                {
                    IndentationMode.Spaces => formattingOptions.WithSpacesIndentationMode(formatting.IndentationSize),
                    IndentationMode.Tabs => formattingOptions.WithTabsIndentationMode(),
                    _ => formattingOptions
                };
            }
            
            optionsBuilder = optionsBuilder.WithFormattingOptions(formattingOptions.GetOptions());

            TOM.TmdlSerializer.SerializeDatabaseToFolder(tomDb, _modelFolder.BasePath, optionsBuilder.GetOptions());

            _modelFolder.MarkWritten();

            return true;
        }

        #endregion

        #region Model Deserialization

        public bool TryDeserialize(out JObject database)
        {
            database = null;

            // handle: no /Model folder
            if (!_modelFolder.Exists()) return false;

            if (_modelFolder.GetFile("model.tmdl").Exists() || _modelFolder.GetFile("model.tmd").Exists())
            {
                database = DeserializeTmdl();
                return true;
            }
            else if(_modelFolder.GetFile(DefaultDatabaseFileName).Exists())
            {
                if (_modelFolder.GetSubfolder(Names.DataSources).Exists())
                {
                    // TODO Support V1 models
                    throw new NotSupportedException("Deserialization of legacy PBIX models is not supported. Please convert the project to the V3 Power BI metadata format first.");
                }

                database = DeserializeLegacy();
                return true;
            }

            return false;
        }

        internal JObject DeserializeTmdl()
        {
            var db = TOM.TmdlSerializer.DeserializeDatabaseFromFolder(_modelFolder.BasePath);
            var tmsl = TOM.JsonSerializer.SerializeDatabase(db,
                new TOM.SerializeOptions { SplitMultilineStrings = true }
            );

            return JObject.Parse(tmsl);
        }

        #endregion
    }
}
