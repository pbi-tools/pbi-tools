// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace PbiTools.Tests
{
    using Utils;

    public abstract class HasTempFolder : IDisposable
    {
        /// <summary>
        /// A temporary folder available for a single test run or test fixture.
        /// </summary>
        protected readonly TempFolder TestFolder = new TempFolder();

        public virtual void Dispose()
        {
            (TestFolder as IDisposable)?.Dispose();
        }
    }
}