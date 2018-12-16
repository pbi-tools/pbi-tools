using System;
using Microsoft.Win32.SafeHandles;

namespace KuduHandles
{
    class SafeGenericHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeGenericHandle()
            : base(true)
        {

        }

        internal SafeGenericHandle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}