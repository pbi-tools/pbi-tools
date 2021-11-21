// Attribution: https://github.com/projectkudu/KuduHandles/tree/8c34ac5/KuduHandles

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PbiTools.Win32
{

    [DebuggerDisplay("{DosFilePath}")]
    class FileHandle
    {
        /*
         * Ideally we will grab all the Network providers from HKLM\SYSTEM\CurrentControlSet\Control\NetworkProvider\Order
         * and look for their Device names under HKLM\SYSTEM\CurrentControlSet\Services\<NetworkProviderName>\NetworkProvider\DeviceName
         * http://msdn.microsoft.com/en-us/library/windows/hardware/ff550865%28v=vs.85%29.aspx
         * However, these providers are generally for devices that are not supported on Azure, so there is no value in adding them.
        */
        private const string NetworkDevicePrefix = "\\Device\\Mup";

        private const string NetworkPrefix = "\\";

        private const int MaxPath = 260;

        private static readonly TimeSpan _ntQueryObjectTimeout = TimeSpan.FromMilliseconds(50);

        private static Dictionary<ushort, string> RawTypeMap { get; set; }

        private static Dictionary<string, string> DeviceMap { get; set; }


        public uint ProcessId { get; }


        public string DosFilePath { get; }

        private static string ResolveDosFilePath(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var volumeNumberLocation = 0;
            for (var j = 0; j < 2 && volumeNumberLocation != -1; j++)
            {
                volumeNumberLocation = name.IndexOf('\\', volumeNumberLocation + 1);
            }

            if (volumeNumberLocation == -1)
            {
                volumeNumberLocation = name.Length;
            }

            var volumeNumber = name.Substring(0, volumeNumberLocation);
            if (DeviceMap.TryGetValue(volumeNumber, out var drive))
            {
                return Regex.Replace(name, Regex.Escape(volumeNumber), drive,
                    RegexOptions.IgnoreCase);
            }

            return null;
        }

        public string Name { get; }

        private static string ResolveName(SafeGenericHandle inProcSafeHandle)
        {
            /*
             * NtQueryObject can hang if called on a synchronous handle that is blocked on an operation (usually a read on pipes, but true for any synchronous handle)
             * The process can also have handles over the network which might be slow to resolve.
             * Therefore, having a timeout on the NtQueryObject is the only solution.
             * Moreover, these threads can't be terminated and the IO operation can't be cancelled. This process will leak threads equal to the number of blocked synchronous handles in the process.
             */
            var _name = default(string);
            ExecuteWithTimeout(
                () => _name = GetNameFromHandle(inProcSafeHandle), 
                () => _name = string.Empty, 
                _ntQueryObjectTimeout);
            return _name;
        }

        private static string ResolveTypeString(ushort rawType, SafeGenericHandle inProcSafeHandle)
        {
            if (RawTypeMap.ContainsKey(rawType))
            {
                return RawTypeMap[rawType];
            }
            else if (inProcSafeHandle != null)
            {
                var typeString = GetTypeFromHandle(inProcSafeHandle);
                RawTypeMap[rawType] = typeString;
                return typeString;
            }

            return null;
        }

        static FileHandle()
        {
            RawTypeMap = new Dictionary<ushort, string>();
            DeviceMap = new Dictionary<string, string>();

            foreach (var drive in Environment.GetLogicalDrives().Select(d => d.Substring(0, 2)))
            {
                var volumnNumber = new StringBuilder(MaxPath);
                if (NativeMethods.QueryDosDevice(drive, volumnNumber, MaxPath) != 0)
                {
                    DeviceMap.Add(volumnNumber.ToString(), drive);
                }
            }
            DeviceMap.Add(NetworkDevicePrefix, NetworkPrefix);
        }

        private FileHandle(uint processId, string name, string dosFilePath)
        {
            ProcessId = processId;
            Name = name;
            DosFilePath = dosFilePath;
        }

        public static bool TryCreate(ulong processId, ulong handle, ushort rawType, out FileHandle fileHandle)
        {
            SafeGenericHandle inProcessSafeHandle;
            fileHandle = null;

            using (var sourceProcessHandle =
                NativeMethods.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE, true,
                    (uint)processId))
            {
                // To read info about a handle owned by another process we must duplicate it into ours
                if (!NativeMethods.DuplicateHandle(sourceProcessHandle,
                    (IntPtr)handle,
                    NativeMethods.GetCurrentProcess(),
                    out inProcessSafeHandle,
                    0,
                    false,
                    DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
                {
                    inProcessSafeHandle = null;
                }
            }

            if (inProcessSafeHandle == null || inProcessSafeHandle.IsInvalid)
                return false;

            using (inProcessSafeHandle)
            {
                var typeString = ResolveTypeString(rawType, inProcessSafeHandle);
                if (!typeString.Equals("File", StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }

                var name = ResolveName(inProcessSafeHandle);
                var dosFilePath = ResolveDosFilePath(name);

                fileHandle = new FileHandle((uint) processId, name, dosFilePath);
                return true;
            }
        }

        private static string GetTypeFromHandle(SafeGenericHandle handle)
        {
            uint length;
            NativeMethods.NtQueryObject(handle,
                OBJECT_INFORMATION_CLASS.ObjectTypeInformation,
                IntPtr.Zero,
                0,
                out length);

            IntPtr ptr = IntPtr.Zero;
            try
            {
                try
                {
                }
                finally
                {
                    ptr = Marshal.AllocHGlobal((int)length);
                }

                if (NativeMethods.NtQueryObject(handle,
                    OBJECT_INFORMATION_CLASS.ObjectTypeInformation,
                    ptr,
                    length,
                    out length) != NTSTATUS.STATUS_SUCCESS)
                {
                    return null;
                }

                var typeInformation =
                    (PUBLIC_OBJECT_TYPE_INFORMATION)
                        Marshal.PtrToStructure(ptr, typeof(PUBLIC_OBJECT_TYPE_INFORMATION));
                return typeInformation.TypeName.ToString();
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static void ExecuteWithTimeout(Action action, Action timeoutAction, TimeSpan timeout)
        {
            var thread = new Thread(action.Invoke) { IsBackground = true };
            thread.Start();
            if (thread.Join(timeout))
            {
                return;
            }

            thread.Abort();
            timeoutAction.Invoke();
        }

        private static string GetNameFromHandle(SafeGenericHandle handle)
        {
            uint length;

            NativeMethods.NtQueryObject(
                handle,
                OBJECT_INFORMATION_CLASS.ObjectNameInformation,
                IntPtr.Zero, 0, out length);
            IntPtr ptr = IntPtr.Zero;
            try
            {
                try { }
                finally
                {
                    ptr = Marshal.AllocHGlobal((int)length);
                }

                if (NativeMethods.NtQueryObject(
                    handle,
                    OBJECT_INFORMATION_CLASS.ObjectNameInformation,
                    ptr, length, out length) != NTSTATUS.STATUS_SUCCESS)
                {
                    return null;
                }

                var unicodeStringName = (UNICODE_STRING)Marshal.PtrToStructure(ptr, typeof(UNICODE_STRING));
                return unicodeStringName.ToString();
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
#endif