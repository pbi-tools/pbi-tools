// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PbiTools.Tests.Deployments
{
    using PbiTools.Deployments;

    public class EnvironmentParameterTests
    {

        [Fact]
        public void Env_params_overwrite_manifest_params_with_same_name()
        {
            var result = DeploymentParameters.CalculateForEnvironment(
                new()
                {
                    Parameters = DeploymentParameters.From(new Dictionary<string, JToken> {
                        { "Param1", "From-Manifest" }
                    })
                },
                new()
                {
                    Parameters = DeploymentParameters.From(new Dictionary<string, JToken> {
                        { "Param1", "From-Environment" }
                    }),
                    Name = "test"
                });

            Assert.Equal((JToken)"From-Environment", result["Param1"]);
        }

        [Fact]
        public void Env_param_can_reference_manifest_params()
        {
            var result = DeploymentParameters.CalculateForEnvironment(
                new()
                {
                    Parameters = DeploymentParameters.From(new Dictionary<string, JToken> {
                        { "Param1", "From-Manifest" }
                    })
                },
                new()
                {
                    Parameters = DeploymentParameters.From(new Dictionary<string, JToken> {
                        { "Param2", "{{Param1}}" }
                    }),
                    Name = "test"
                });

            Assert.Equal((JToken)"From-Manifest", result["Param2"]);
        }

        [Fact]
        public void Env_params_can_reference_environment_variables()
        {
            var envTestKey = Guid.NewGuid().ToString();
            Environment.SetEnvironmentVariable(envTestKey, "_TEST_");

            var result = DeploymentParameters.CalculateForEnvironment(
                new()
                {
                    Parameters = default
                },
                new()
                {
                    Parameters = DeploymentParameters.From(new Dictionary<string, JToken> {
                        { "Param1", $"--%{envTestKey}%--" }
                    }),
                    Name = "test"
                });

            Assert.Equal((JToken)"--_TEST_--", result["Param1"]);
        }

        [Fact]
        public void Env_params_can_reference_system_params()
        {
            var result = DeploymentParameters.CalculateForEnvironment(
                new()
                {
                    Parameters = default
                },
                new()
                {
                    Parameters = DeploymentParameters.From(new Dictionary<string, JToken> {
                        { "Param1", "--{{SYSTEM-PARAM}}--" }
                    }),
                    Name = "test"
                },
                ("SYSTEM-PARAM", "From-System"));

            Assert.Equal((JToken)"--From-System--", result["Param1"]);
        }

        [Fact]
        public void Handles_missing_manifest_params() 
        {
            var systemParams = DeploymentParameters.GetSystemParameters("test");
            var copy = systemParams.ToDictionary(x => x.Key, x => DeploymentParameter.From(x.Value));
            copy.Add("Param1", (JToken)42);

            var result = DeploymentParameters.CalculateForEnvironment(
                new()
                {
                    Parameters = default
                },
                new()
                {
                    Parameters = DeploymentParameters.From(new Dictionary<string, JToken> {
                        { "Param1", 42 }
                    }), 
                    Name = "test"
                });

            Assert.Equal(copy, result);
        }

        [Fact]
        public void Handles_missing_env_params()
        {
            var systemParams = DeploymentParameters.GetSystemParameters("test");
            var copy = systemParams.ToDictionary(x => x.Key, x => DeploymentParameter.From(x.Value));
            copy.Add("Param1", (JToken)"foo");

            var result = DeploymentParameters.CalculateForEnvironment(
                new()
                {
                    Parameters = DeploymentParameters.From(new Dictionary<string, JToken> {
                        { "Param1", "foo" }
                    })
                },
                new()
                {
                    Parameters = default,
                    Name = "test"
                });

            Assert.Equal(copy, result);
        }

        [Fact]
        public void Handles_missing_manifest_and_env_params() 
        {
            var systemParams = DeploymentParameters.GetSystemParameters("test");
            var copy = systemParams.ToDictionary(x => x.Key, x => DeploymentParameter.From(x.Value));

            var result = DeploymentParameters.CalculateForEnvironment(
                new() { Parameters = default },
                new() { Parameters = default, Name = "test" });

            Assert.Equal(copy, result);
        }

    }
}
