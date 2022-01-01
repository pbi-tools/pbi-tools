// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Xunit;

namespace PbiTools.Tests
{
    /// <summary>
    /// Skips the tests and marks it as Not Implemented.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TestNotImplementedAttribute : FactAttribute
    {
        public TestNotImplementedAttribute()
        {
            base.Skip = "Test Not Implemented";
        }
    }
}
