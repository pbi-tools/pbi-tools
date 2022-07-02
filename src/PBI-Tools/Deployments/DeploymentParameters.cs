// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbiTools.Deployments
{
    /// <summary>
    /// Defines a set of parameters used inside a Deployment Manifest.
    /// </summary>
    [JsonConverter(typeof(DeploymentParametersConverter))]
    public class DeploymentParameters : ReadOnlyDictionary<string, DeploymentParameter>
    {
        public static class Names
        {
            // TODO Document those params and their implementation for each deployment mode!

            /* Common */
            public const string ENVIRONMENT = nameof(ENVIRONMENT);
            public const string PBIXPROJ_FOLDER = nameof(PBIXPROJ_FOLDER);
            public const string FILE_NAME = nameof(FILE_NAME);
            public const string FILE_NAME_WITHOUT_EXT = nameof(FILE_NAME_WITHOUT_EXT);
            
            /* Report */
            public const string PBIXPROJ_NAME = nameof(PBIXPROJ_NAME);
            public const string FILE_PATH = nameof(FILE_PATH);
        }

        private DeploymentParameters(IDictionary<string, DeploymentParameter> parameters) : base(parameters) { }

        internal new IDictionary<string, DeploymentParameter> Dictionary => base.Dictionary;

        public static DeploymentParameters From(IDictionary<string, JToken> dict) =>
            new(dict.ToDictionary(
                x => x.Key,
                x => DeploymentParameter.FromJson(x.Value)
            ));
    }

    public enum DeploymentParameterValueType
    { 
        Text,
        Null,
        Number,
        Bool,
        Expression
    }

    public class DeploymentParameter
    {
        
        public DeploymentParameterValueType ValueType { get; }
        public object Value { get; }

        private DeploymentParameter(DeploymentParameterValueType type, object value)
        {
            this.ValueType = type;
            this.Value = value;
        }

        public static DeploymentParameter FromJson(JToken token)
        {
            if (token is JValue value)
            {
                var result = value.Type switch
                {
                    JTokenType.Float or JTokenType.Integer                       => (DeploymentParameterValueType.Number,     value.Value ?? default(int)),
                    JTokenType.Boolean                                           => (DeploymentParameterValueType.Bool,       value.Value ?? default(bool)),
                    JTokenType.Null                                              => (DeploymentParameterValueType.Null,       default(object)),
                    JTokenType.String when value.Value<string>().StartsWith("#") => (DeploymentParameterValueType.Expression, (string)value.Value),
                    JTokenType.String                                            => (DeploymentParameterValueType.Text,       (string)value.Value),
                    _ => throw new NotSupportedException($"This value is not supported as a parameter value: '{value}' (value type: '{value.Type}').")
                };
                return new(result.Item1, result.Item2);
            }
            else if (token is JObject obj
                && obj.TryGetValue("Value", StringComparison.InvariantCultureIgnoreCase, out var _value)
                && _value is JValue value2)
            {
                return FromJson(value2);
            }

            throw new NotSupportedException($"This value is not supported as a parameter value: '{token}' (token type: '{token.Type}').");
        }

        public static DeploymentParameter From(string value) => (value == null)
            ? new(DeploymentParameterValueType.Null, null)
            : new(DeploymentParameterValueType.Text, value);

        public DeploymentParameter CloneWithValue(object value) =>
            new(this.ValueType, value);
            
        public JToken ToJson() => this.ValueType switch
        {
            // TODO Handle complex params (w/ additional attributes)
            DeploymentParameterValueType.Null => JValue.CreateNull(),
            _ => new JValue(this.Value)
        };

        /// <summary>
        /// Converts the parameter value to a valid M expression.
        /// </summary>
        public string ToMString() => this.ValueType switch
        {
            DeploymentParameterValueType.Null => "null",
            DeploymentParameterValueType.Text => $"\"{new StringBuilder((string)this.Value).Replace("\"", "\"\"")}\"",
            _ => Convert.ToString(this.Value, System.Globalization.CultureInfo.InvariantCulture)
        };

        public override string ToString() => $"{Value}";

    }

    public class DeploymentParametersConverter : JsonConverter<DeploymentParameters>
    {
        public override void WriteJson(JsonWriter writer, DeploymentParameters value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            foreach (var key in value.Keys)
            {
                writer.WritePropertyName(key);
                value[key].ToJson().WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        public override DeploymentParameters ReadJson(JsonReader reader, Type objectType, DeploymentParameters existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var dict = serializer.Deserialize<IDictionary<string, JToken>>(reader);
            return DeploymentParameters.From(dict);
        }
    }

}