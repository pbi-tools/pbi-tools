// Attribution: https://github.com/projectkudu/KuduHandles/tree/8c34ac5/KuduHandles

#if NETFRAMEWORK
using System;
using Microsoft.Win32.SafeHandles;

namespace PbiTools.Win32
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
#endif