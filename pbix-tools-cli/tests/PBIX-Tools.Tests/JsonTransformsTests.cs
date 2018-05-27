using Newtonsoft.Json.Linq;
using PbixTools.Serialization;
using Xunit;

namespace PbixTools.Tests
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
