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
            public const string PBITOOLS_VERSION = nameof(PBITOOLS_VERSION);
            public const string PBIXPROJ_FOLDER = nameof(PBIXPROJ_FOLDER);
            public const string FILE_NAME = nameof(FILE_NAME);
            public const string FILE_NAME_WITHOUT_EXT = nameof(FILE_NAME_WITHOUT_EXT);

            /* Report */
            public const string PBIXPROJ_NAME = nameof(PBIXPROJ_NAME);
            public const string FILE_PATH = nameof(FILE_PATH);
        }

        /// <summary>
        /// Generates common system parameters for the specified environment.
        /// </summary>
        public static IDictionary<string, string> GetSystemParameters(string environment) => new Dictionary<string, string> {
            { Names.ENVIRONMENT, environment },
            { Names.PBITOOLS_VERSION, AssemblyVersionInformation.AssemblyInformationalVersion },
        };

        private DeploymentParameters(IDictionary<string, DeploymentParameter> parameters) : base(parameters) { }

        internal new IDictionary<string, DeploymentParameter> Dictionary => base.Dictionary;

        public static DeploymentParameters From(IDictionary<string, JToken> dict) =>
            new(dict.ToDictionary(
                x => x.Key,
                x => DeploymentParameter.FromJson(x.Value)
            ));

        /// <summary>
        /// Calculates effective environment parameters taking into account
        /// declared manifest parameters, declared environment parameters, as well as system parameters.
        /// ENV expansion is performed on both manifest and environment parameters.
        /// Furthermore, manifest and environment parameters can reference system parameters,
        /// and environment parameters can reference manifest parameters.
        /// </summary>
        public static IDictionary<string, DeploymentParameter> CalculateForEnvironment(
            PbiDeploymentManifest manifest,
            PbiDeploymentEnvironment environment,
            params (string Key, string Value)[] additionalSystemParams)
        {
            var systemParams = additionalSystemParams.Aggregate(
                GetSystemParameters(environment.Name),
                (dict, x) => dict.With(x.Key, x.Value)
            );

            var manifestParams = systemParams.Aggregate(   // System params overwrite Manifest params
                manifest.Parameters
                    .ExpandEnv()
                    .ExpandParameters(systemParams),
                (dict, x) =>
                {
                    dict[x.Key] = DeploymentParameter.From(x.Value);
                    return dict;
                }
            );

            return environment.Parameters
                .ExpandEnv()
                .ExpandParameters(systemParams)
                .ExpandParameters(manifestParams)
                .Aggregate(    // ENV params overwrite Manifest params
                    manifestParams,
                    (dict, x) =>
                    {
                        dict[x.Key] = x.Value;
                        return dict;
                    }
                );
        }

        /// <summary>
        /// Calculates effective environment parameters taking into account
        /// declared manifest parameters, declared environment parameters, as well as system parameters.
        /// ENV expansion is performed on both manifest and environment parameters.
        /// Furthermore, manifest and environment parameters can reference system parameters,
        /// and environment parameters can reference manifest parameters.
        /// </summary>
        public static IDictionary<string, DeploymentParameter> CalculateForEnvironment(
            PbiDeploymentManifest manifest,
            PbiDeploymentEnvironment environment,
            IDictionary<string, string> additionalSystemParams)
        =>
            CalculateForEnvironment(manifest, environment, additionalSystemParams.Select(x => (x.Key, x.Value)).ToArray());
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

        public static implicit operator DeploymentParameter(JToken json) => FromJson(json);

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

        public override bool Equals(object obj) =>
            obj is DeploymentParameter other
            ? this.ValueType == other.ValueType && this.Value.Equals(other.Value)
            : base.Equals(obj);

        public override int GetHashCode() => (this.ValueType, this.ValueType).GetHashCode();

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