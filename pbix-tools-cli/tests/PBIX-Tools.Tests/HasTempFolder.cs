using System;

namespace PbixTools.Tests
{
    public abstract class HasTempFolder : IDisposable
    {
        protected readonly TempFolder TestFolder = new TempFolder();

        public virtual void Dispose()
        {
            (TestFolder as IDisposable)?.Dispose();
        }
    }
}