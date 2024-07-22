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
