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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerBI.Api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PbiTools.Deployments
{

    /// <summary>
    /// A collection of refresh object expressions with their respective refresh type.
    /// An expression can reference a table or a partition. Partitions are prefixed with their table name,
    /// separated using the <c>|</c> pipe character, for instance: <c>Customers|Partition-1</c>.
    /// Furthermore, expressions can contain wildcards (<c>*, ?</c>), either in the table or the partition segment.
    /// For example, the expression <c>*|Partition*</c> matches all partitions starting with "Partition" across all tables.
    /// Any tables/partitions not explicitly matched will be refreshed using the default <see cref="PbiDeploymentOptions.RefreshOptions.Type"/>
    /// specified under options/refresh/type.
    /// In order to remove any tables/partitions from the refresh, specify "None" as the type.
    /// Other valid refresh types are: <c>Automatic</c>, <c>Full</c>, <c>Calculate</c>, <c>DataOnly</c>, <c>ClearValues</c>, and <c>Defragment</c>.
    /// </summary>
    [JsonConverter(typeof(RefreshObjectsConverter))]
    public class RefreshObjects : IEnumerable<RefreshObject>
    {
        private RefreshObject[] Objects { get; set; }

        private RefreshObjects() { }

        public bool IsEmpty => !(Objects?.Length > 0);

        public static RefreshObjects FromJson(JObject json) =>
            json == null
            ? default
            : new() { Objects = json.Properties().Select(RefreshObject.FromJson).ToArray() };


        IEnumerator<RefreshObject> IEnumerable<RefreshObject>.GetEnumerator()
            => (Objects as IEnumerable<RefreshObject>).GetEnumerator(); 

        IEnumerator IEnumerable.GetEnumerator()
            => Objects.GetEnumerator();
    }

    public class RefreshObject
    {
        public static readonly string NoneRefreshType = "None";

        private RefreshObject() { }

        public DatasetRefreshType? RefreshType { get; private set; }

        public RefreshObjectType ObjectType { get; private set; }

        public string TableExpression { get; private set; }

        public string PartitionExpression { get; private set; }

        public string OriginalString { get; private set; }

        public bool SkipRefresh => !RefreshType.HasValue;

        public JProperty ToJson() => new (OriginalString, RefreshType?.ToString() ?? NoneRefreshType);

        public static RefreshObject FromJson(JProperty property)
        {
            var result = new RefreshObject { OriginalString = property.Name };
            var split = property.Name.Split('|');

            /* Object Type */

            if (split.Length == 1)
            {
                result.ObjectType = RefreshObjectType.Table;
                result.TableExpression = property.Name;
            }
            else if (split.Length > 2) {
                throw new DeploymentException($"The refresh object expression for '{property.Name}' is invalid. It may only contain one '|' separator. (Path: {property.Path})");
            }
            else {
                result.ObjectType = RefreshObjectType.Partition;
                result.TableExpression = split[0];
                result.PartitionExpression = split[1];
            }

            /* Refresh Type */

            if (property.Value.Type != JTokenType.String)
                throw new DeploymentException($"The refresh object expression for '{property.Name}' has an invalid json type. Only string values are allowed. (Path: {property.Value.Path})");

            var valueRaw = property.Value.Value<string>();
            if (!valueRaw.Equals(NoneRefreshType, StringComparison.InvariantCultureIgnoreCase))
                result.RefreshType = valueRaw;

            return result;
        }

    }

    public enum RefreshObjectType
    {
        Model = -1,
        Unspecified = default,
        Table,
        Partition
    }

    public class RefreshObjectsConverter : JsonConverter<RefreshObjects>
    {
        public override RefreshObjects ReadJson(JsonReader reader, System.Type objectType, RefreshObjects existingValue, bool hasExistingValue, JsonSerializer serializer)
            => RefreshObjects.FromJson(JObject.Load(reader));

        public override void WriteJson(JsonWriter writer, RefreshObjects value, JsonSerializer serializer)
        {
            if (value == null)
                writer.WriteNull();
            else
                new JObject(value.Select(o => o.ToJson())).WriteTo(writer);
        }

    }
}
