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

namespace PbiTools.Serialization
{
    /// <summary>
    /// Transforms a Power BI package part, represented as an instance of <see cref="T"/>,
    /// into (Serialize) and back from (Deserialize) the <c>PbixProj</c> file format.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    // ReSharper disable once InconsistentNaming
    public interface IPowerBIPartSerializer<T>
    {
        bool Serialize(T content);
        bool TryDeserialize(out T part);

        /// <summary>
        /// The file path a part is serialized to and from for simple parts; the base folder path for parts serialized into a folder structure.
        /// </summary>
        string BasePath { get; }
    }

    public static class PowerBIPartSerializerExtensions
    {

        /// <summary>
        /// Deserializes a PBIX part to the corresponding PbixModel data type in a safe way.
        /// Returns the type's default value if the deserialization is not possible or fails.
        /// </summary>
        public static T DeserializeSafe<T>(this IPowerBIPartSerializer<T> serializer, bool isOptional = true)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            
            var logger = Serilog.Log.ForContext(serializer.GetType());
            logger.Verbose("Attempting to deserialize part at path: {Path}", serializer.BasePath);
            
            try
            {
                if (serializer.TryDeserialize(out var part))
                {
                    logger.Debug("Successfully deserialized part: {Path}", serializer.BasePath);
                    return part;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An unhandled exception occurred during deserialization.");
            }

            if (!isOptional) throw new PbixProjSerializationException(
                $"The PBIX part at '{serializer.BasePath}' could not be deserialized and is marked as not optional.");

            return default(T);
        }
    }

    [System.Serializable]
    public class PbixProjSerializationException : System.Exception
    {
        public PbixProjSerializationException() { }
        public PbixProjSerializationException(string message) : base(message) { }
        public PbixProjSerializationException(string message, System.Exception inner) : base(message, inner) { }
        protected PbixProjSerializationException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
