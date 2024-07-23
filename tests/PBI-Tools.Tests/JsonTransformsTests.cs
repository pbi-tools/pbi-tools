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

using Newtonsoft.Json.Linq;
using PbiTools.Serialization;
using Xunit;

namespace PbiTools.Tests
{
    public class JsonTransformsTests
    {
        [Fact]
        public void NormalizeNumbers__Converts_Floats_To_Ints_if_lossfree()
        {
            JToken token1 = JToken.Parse("200.0");
            Assert.Equal(JTokenType.Float, token1.Type);

            var result = token1.NormalizeNumbers();

            Assert.Equal(JTokenType.Integer, result.Type);
            Assert.Equal("200", result.ToString());
        }

        [Fact]
        public void NormalizeNumbers__Allow_for_tolerance_of_10e_neg4()
        {
            JToken token1 = JToken.Parse("200.00009");
            Assert.Equal(JTokenType.Float, token1.Type);

            var result = token1.NormalizeNumbers();

            Assert.Equal(JTokenType.Integer, result.Type);
            Assert.Equal("200", result.ToString());
        }

        [Fact]
        public void NormalizeNumbers__Keeps_float_if_not_lossfree()
        {
            JToken token1 = JToken.Parse("200.22");
            Assert.Equal(JTokenType.Float, token1.Type);

            var result = token1.NormalizeNumbers();

            Assert.Equal(JTokenType.Float, result.Type);
            Assert.Equal("200.22", result.ToString());
        }

    }
}
