// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace PbiTools.Deployments
{

    [Serializable]
    public class DeploymentException : Exception
    {
        public DeploymentException() { }
        public DeploymentException(string message) : base(message) { }
        public DeploymentException(string message, System.Exception inner) : base(message, inner) { }
        protected DeploymentException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }

        public static DeploymentException From(Microsoft.Identity.Client.MsalServiceException msalException)
        {
            JObject msalResponse = default;
            try
            {
                msalResponse = JObject.Parse(msalException.ResponseBody);
                DeploymentManager.Log.Debug(msalException, "A MSAL Service Exception has occurred.");
            }
            catch
            { }

            return msalResponse == null
                ? new(msalException.GetType().Name, msalException)
                : new($"Authentication error:\n{msalResponse.ToString(Newtonsoft.Json.Formatting.Indented)}");
        }
    }
}