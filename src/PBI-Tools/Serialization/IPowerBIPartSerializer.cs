// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace PbiTools.Serialization
{
    /// <summary>
    /// Transforms a Power BI package part, that has been converted to <see cref="T"/>,
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
        public static T DeserializeSafe<T>(this IPowerBIPartSerializer<T> serializer)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            
            try
            {
                if (serializer.TryDeserialize(out var part))
                    return part;
            }
            catch (Exception ex)
            {
                var logger = Serilog.Log.Logger.ForContext(serializer.GetType());
                logger.Error(ex, "An unhandled exception occurred during deserialization.");
            }

            return default(T);
        }
    }
}