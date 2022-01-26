// Attribution: https://github.com/projectkudu/KuduHandles/tree/8c34ac5/KuduHandles

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace PbiTools.Win32
{
    class SystemUtility
    {
        public static IEnumerable<FileHandle> GetHandles(int[] processIds)
        {
            var longProcIds = processIds.Select(Convert.ToUInt64).OrderBy(x => x).ToArray();
            uint length = 0x10000;
            IntPtr ptr = IntPtr.Zero;
            try
            {
                try { }
                finally
                {
                    ptr = Marshal.AllocHGlobal((int)length);
                }


                uint returnLength;
                NTSTATUS result;
                while ((result = NativeMethods.NtQuerySystemInformation(
                    SYSTEM_INFORMATION_CLASS.SystemHandleInformation, ptr, length, out returnLength)) ==
                       NTSTATUS.STATUS_INFO_LENGTH_MISMATCH)
                {
                    length = ((returnLength + 0xffff) & ~(uint)0xffff);
                    try { }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                        ptr = Marshal.AllocHGlobal((int)length);
                    }
                }

                if (result != NTSTATUS.STATUS_SUCCESS)
                    yield break;

                long handleCount = Marshal.ReadInt64(ptr);
                int offset = sizeof(long) + sizeof(long);
                int size = Marshal.SizeOf(typeof(SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX));
                for (int i = 0; i < handleCount; i++)
                {
                    var handleEntry =
                        (SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX)Marshal.PtrToStructure(
                        IntPtr.Add(ptr, offset), typeof(SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX));

                    if (Array.BinarySearch(longProcIds, handleEntry.UniqueProcessId) > -1 
                        && FileHandle.TryCreate(handleEntry.UniqueProcessId, handleEntry.HandleValue, handleEntry.ObjectTypeIndex, out var fileHandle))
                    {
                        yield return fileHandle;
                    }

                    offset += size;
                }
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

    }
}
#endif