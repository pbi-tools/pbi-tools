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
        public static ReadOnlyDictionary<string, string> GetSystemParameters(string environment) => new(new Dictionary<string, string> {
            { Names.ENVIRONMENT, environment },
            { Names.PBITOOLS_VERSION, AssemblyVersionInformation.AssemblyInformationalVersion },
        });

        internal DeploymentParameters(IDictionary<string, DeploymentParameter> parameters) : base(parameters) 
        { }

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
        public static ReadOnlyDictionary<string, DeploymentParameter> CalculateForEnvironment(
            PbiDeploymentManifest manifest,
            PbiDeploymentEnvironment environment,
            params (string Key, string Value)[] additionalSystemParams)
        {
            // Start with SYSTEM params
            var systemParams = additionalSystemParams.Aggregate(
                new Dictionary<string, string>(GetSystemParameters(environment.Name)),
                (dict, x) => dict.With(x.Key, x.Value)
            );

            // Then expand and add MANIFEST params
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

            // Finally expand and add ENVIRONMENT params
            // Return as read-only dictionary
            return new ReadOnlyDictionary<string, DeploymentParameter>(environment.Parameters
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
                ));
        }

        /// <summary>
        /// Calculates effective environment parameters taking into account
        /// declared manifest parameters, declared environment parameters, as well as system parameters.
        /// ENV expansion is performed on both manifest and environment parameters.
        /// Furthermore, manifest and environment parameters can reference system parameters,
        /// and environment parameters can reference manifest parameters.
        /// </summary>
        public static ReadOnlyDictionary<string, DeploymentParameter> CalculateForEnvironment(
            PbiDeploymentManifest manifest,
            PbiDeploymentEnvironment environment,
            IDictionary<string, string> additionalSystemParams)
        =>
            CalculateForEnvironment(
                manifest, 
                environment, 
                additionalSystemParams.Select(x => (x.Key, x.Value)).ToArray()
            );
    }

    public enum DeploymentParameterValueType
    { 
        Text,
        Null,
        Number,
        Bool,
        Expression
    }

    public readonly struct DeploymentParameter : IEquatable<DeploymentParameter>
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
            
            if (token is JObject obj
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

        #region Equality

        public bool Equals(DeploymentParameter other) => ValueType == other.ValueType && Equals(Value, other.Value);

        public override bool Equals(object obj) => obj is DeploymentParameter other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)ValueType, Value);

        #endregion
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
