// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;

namespace PbiTools.PowerBI
{
    /// <summary>
    /// Converts back-and-forth between a native Power BI package part Stream
    /// and a specific representation of the underlying content as <see cref="T"/>.
    /// </summary>
    /// <typeparam name="T">A type that represents a Power BI package part content without any dependency on the Power BI API.</typeparam>
    // ReSharper disable once InconsistentNaming
    public interface IPowerBIPartConverter<T>
        where T : class
    {
        T FromPackagePart(Func<Stream> part, string contentType = null);
        Func<Stream> ToPackagePart(T content);
        Uri PartUri { get; }
        bool IsOptional { get; }
        string ContentType { get; }
    }

    public static class StreamExtensions
    {
        public static bool TryGetStream(this Func<Stream> part, out Stream stream)
        { 
            stream = (part == null) ? null : part();
            return (stream != null);
        }
    }
}