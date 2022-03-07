// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if NETFRAMEWORK
using System.Collections.Generic;
using System.Text;
using Microsoft.Mashup.Host.Document;
using Microsoft.Mashup.Host.Document.DataSourceDiscovery;
using Moq;
using Newtonsoft.Json.Linq;
using PbiTools.Serialization;
using Serilog;

namespace PbiTools.Tabular
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

        public static string GenerateSectionDocumentFromModel(JObject database)
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

            return m;
        }
        
        public static JArray GenerateDataSources(JObject database)
        {
            var dataSources = new Dictionary<string, JObject>();

            var engine = Microsoft.Mashup.Engine.Host.Engines.Version1;
            var m = GenerateSectionDocumentFromModel(database);
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
#endif