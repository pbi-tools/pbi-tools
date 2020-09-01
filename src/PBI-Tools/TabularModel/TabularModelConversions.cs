// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Text;
using Microsoft.Mashup.Host.Document;
using Microsoft.Mashup.Host.Document.DataSourceDiscovery;
using Newtonsoft.Json.Linq;
using PbiTools.Serialization;
using Serilog;
using Moq;

namespace PbiTools.TabularModel
{
    public class TabularModelConversions
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<TabularModelConversions>();

        static TabularModelConversions()
        {
            // Needed for DataSourceDiscoveryVisitor.CurrentLibrary
            var DI = DependencyInjectionService.Get();
            if (!DI.IsRegistered<IFeatureSwitchManager>())
                DI.RegisterType<IFeatureSwitchManager, NoOpFeatureSwitchManager>();
            if (!DI.IsRegistered<ITracingHost>())
                DI.RegisterInstance<ITracingHost>(new Mock<ITracingHost>().Object);
        }

        public static JArray GenerateDataSources(JObject database)
        {
            var tablePartitionExpr = database.SelectTokens("model.tables[*].partitions[?(@.source.type == 'm')]");
            var sharedExpr = database.SelectTokens("model.expressions[?(@.kind == 'm')]");

            var mBldr = new StringBuilder("section Section1;");
            foreach (var expr in tablePartitionExpr)
            {
                mBldr.AppendLine();
                mBldr.AppendLine();
                mBldr.Append($"shared #\"{expr.Parent.Parent.Parent.Value<string>("name")}\" = ");
                mBldr.Append(TabularModelSerializer.ConvertExpression(expr.SelectToken("source.expression")));
                mBldr.Append(";");
            }
            foreach (var expr in sharedExpr)
            {
                mBldr.AppendLine();
                mBldr.AppendLine();
                mBldr.Append($"shared #\"{expr.Value<string>("name")}\" = ");
                mBldr.Append(TabularModelSerializer.ConvertExpression(expr.SelectToken("expression")));
                mBldr.Append(";");
            }

            var m = mBldr.ToString();

            Log.Verbose("M Document Generated: {M}", m);

            //var expressions = new Dictionary<string, string>();
            //IEngine mEngine = Engines.Version1;

            //var tokens = mEngine.Tokenize(m);
            //var doc = mEngine.Parse(tokens, new TextDocumentHost(m), err => { });

            //var valueOnly = false;
            //if (doc is ISectionDocument sectionDoc)
            //{
            //    foreach (var export in sectionDoc.Section.Members)
            //    {
            //        if (expressions.ContainsKey(export.Name.Name)) continue;
            //        if (valueOnly)
            //            expressions.Add(export.Name.Name, tokens.GetText(export.Value.Range.Start, export.Value.Range.End).ToString());
            //        else
            //            expressions.Add(export.Name.Name, tokens.GetText(export.Range.Start, export.Range.End).ToString());
            //    }
            //}

            var dataSources = new Dictionary<string, JObject>();

            var engine = Microsoft.Mashup.Engine.Host.Engines.Version1;
            var discoveries = DataSourceDiscoveryVisitor.FindDataSourcesForDocument(m);

            foreach (var mashupDiscovery in discoveries)
            {
                if (engine.TryCreateLocationFromResource(mashupDiscovery.Resource, true, out var location))
                {
                    Log.Verbose(location.ToJson());
                    
                    var name = $"{location.ResourceKind}/{location.FriendlyName}"; // TODO AAS seems to remove dots from server name .. shall we follow same pattern?
                    if (!dataSources.ContainsKey(name))
                    {
                        dataSources.Add(name, new JObject
                        {
                            { "type", "structured" },
                            { "name", name },
                            { "connectionDetails", JObject.Parse(location.ToJson()) },
                            { "credential", new JObject {  // Assuming those will be populated separately or via refresh overrides
                                //	{ "AuthenticationKind", "ServiceAccount" }
                            }}
                        });
                    }
                }
            }

            return new JArray(dataSources.Values);
        }
    }
}
