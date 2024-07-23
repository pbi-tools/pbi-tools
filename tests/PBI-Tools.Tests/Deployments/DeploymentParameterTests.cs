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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace PbiTools.Tests.Deployments
{
    using PbiTools.Deployments;

    public class DeploymentParameterTests
    {
        private static JsonSerializer jsonSerializer = new();

        private static string TestParams = """
            {
                "Number": 1,
                "Number2": 0.4,
                "Null": null,
                "String": "foo",
                "Bool": true,
                "Date": "#date(2022, 6, 1)",
                "Duration": "#duration(5, 0, 0, 0)"
            }
            """;

        private static string TestParamsComplex = """
            {
                "Number": { "value": 1},
                "Number2": { "Value": 0.4 },
                "Null": { "VALUE": null },
                "String": { "value": "foo" },
                "Bool": { "value": true },
                "Date": { "Value": "#date(2022, 6, 1)" },
                "Duration": { "value": "#duration(5, 0, 0, 0)" }
            }
            """;

        public static IEnumerable<object[]> TestData() => new List<object[]>
        { 
            new object[] { "Number", DeploymentParameterValueType.Number, 1L },
            new object[] { "Number2", DeploymentParameterValueType.Number, 0.4 },
            new object[] { "Null", DeploymentParameterValueType.Null, null },
            new object[] { "String", DeploymentParameterValueType.Text, "foo" },
            new object[] { "Bool", DeploymentParameterValueType.Bool, true },
            new object[] { "Date", DeploymentParameterValueType.Expression, "#date(2022, 6, 1)" },
            new object[] { "Duration", DeploymentParameterValueType.Expression, "#duration(5, 0, 0, 0)" },
        };

        [Theory]
        [MemberData(nameof(TestData))]
        public void Can_parse_simple_param_values(string name, DeploymentParameterValueType valueType, object value)
        {
            using var reader = new JsonTextReader(new StringReader(TestParams));
            var parameters = jsonSerializer.Deserialize<DeploymentParameters>(reader);

            var parameter = parameters[name];

            Assert.Equal(valueType, parameter.ValueType);
            Assert.Equal(value, parameter.Value);
        }


        [Theory]
        [MemberData(nameof(TestData))]
        public void Can_parse_complex_param_values(string name, DeploymentParameterValueType valueType, object value)
        {
            using var reader = new JsonTextReader(new StringReader(TestParamsComplex));
            var parameters = jsonSerializer.Deserialize<DeploymentParameters>(reader);

            var parameter = parameters[name];

            Assert.Equal(valueType, parameter.ValueType);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void Escapes_double_quotes_in_M_expression()
        {
            var param = DeploymentParameter.From("""
                abc"ABC""def
                """);

            Assert.Equal("""""
                "abc""ABC""""def"
                """"", param.ToMString());
        }

        [Fact]
        public void Expressions_are_returned_verbatim_as_M_string()
        {
            var param = DeploymentParameter.FromJson("#date(2000, 1, 1)");

            Assert.Equal("#date(2000, 1, 1)", param.ToMString());
        }

        [Fact]
        public void Text_value_are_enclosed_in_double_quotes_as_M_string()
        {
            var param = DeploymentParameter.FromJson("abcdefg");

            Assert.Equal("""
                "abcdefg"
                """, param.ToMString());
        }
    }
}
