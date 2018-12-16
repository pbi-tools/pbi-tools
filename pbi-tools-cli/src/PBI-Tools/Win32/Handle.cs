using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace KuduHandles
{
    public enum HandleType
    {
        Unknown,
        Other,
        File,
        Directory
    }

    class Handle
    {
        /*
         * Ideally we will grab all the Network providers from HKLM\SYSTEM\CurrentControlSet\Control\NetworkProvider\Order
         * and look for their Device names under HKLM\SYSTEM\CurrentControlSet\Services\<NetworkProviderName>\NetworkProvider\DeviceName
         * http://msdn.microsoft.com/en-us/library/windows/hardware/ff550865%28v=vs.85%29.aspx
         * However, these providers are generally for devices that are not supported on Azure, so there is no value in adding them.
        */
        private const string NetworkDevicePrefix = "\\Device\\Mup";

        private const string NetworkPrefix = "\\";

        private const string SiteWwwroot = "SITE\\WWWROOT";

        private const string HomeEnvironmentVariable = "%HOME%";

        private const int MaxPath = 260;

        private readonly TimeSpan _ntQueryObjectTimeout = TimeSpan.FromMilliseconds(50);

        private static Dictionary<ushort, string> RawTypeMap { get; set; }

        public static string HomePath { get; set; }

        public static string UncPath { get; set; }

        private static Dictionary<string, string> DeviceMap { get; set; }

        public uint ProcessId { get; private set; }

        public uint RawHandleValue { get; private set; }

        public ushort RawType { get; private set; }

        private readonly SafeGenericHandle _inProcessSafeHandle;

        private string _dosFilePath;

        public string DosFilePath
        {
            get
            {
                if (_dosFilePath == null && !String.IsNullOrEmpty(Name))
                {
                    int volumnNumberLocation = 0;
                    for (int j = 0; j < 2 && volumnNumberLocation != -1; j++)
                    {
                        volumnNumberLocation = Name.IndexOf('\\', volumnNumberLocation + 1);
                    }

                    if (volumnNumberLocation == -1)
                    {
                        volumnNumberLocation = Name.Length;
                    }

                    var volumnNumber = Name.Substring(0, volumnNumberLocation);
                    string drive;
                    if (DeviceMap.TryGetValue(volumnNumber, out drive))
                    {
                        _dosFilePath = Regex.Replace(Name, Regex.Escape(volumnNumber), drive,
                            RegexOptions.IgnoreCase);

                        if (UncPath != null && HomePath != null && _dosFilePath != null)
                        {
                            _dosFilePath = Regex.Replace(_dosFilePath, Regex.Escape(UncPath), HomePath,
                                RegexOptions.IgnoreCase);
                        }
                    }
                }

                return _dosFilePath;
                }
        }

        private string _name;

        public string Name
        {
            get
            {
                if (_name == null && _inProcessSafeHandle != null)
                {
                    /*
                     * NtQueryObject can hang if called on a synchronous handle that is blocked on an operation (usually a read on pipes, but true for any synchronous handle)
                     * The process can also have handles over the network which might be slow to resolve.
                     * Therefore, having a timeout on the NtQueryObject is the only solution.
                     * Moreover, these threads can't be terminated and the IO operation can't be cancelled. This process will leak threads equal to the number of blocked synchronous handles in the process.
                     */
                    ExecuteWithTimeout(() => Name = GetNameFromHandle(_inProcessSafeHandle), () => Name = String.Empty, _ntQueryObjectTimeout);
                }
                return _name;
            }

            private set
            {
                _name = value;
            }
        }

        private string _typeString;

        public string TypeString
        {
            get
            {
                if (RawTypeMap.ContainsKey(RawType))
                {
                    _typeString = RawTypeMap[RawType];
                }
                else if (_inProcessSafeHandle != null)
                {
                    _typeString = GetTypeFromHandle(_inProcessSafeHandle);
                    RawTypeMap[RawType] = _typeString;
                }

                return _typeString;
            }
        }

        public HandleType Type
        {
            get
            {
                switch (TypeString)
                {
                    case null:
                        return HandleType.Unknown;
                    case "File":
                        return HandleType.File;
                    case "Directory":
                        return HandleType.Directory;
                    default:
                        return HandleType.Other;
                }
            }
        }

        static Handle()
        {
            RawTypeMap = new Dictionary<ushort, string>();
            HomePath = Environment.ExpandEnvironmentVariables(HomeEnvironmentVariable) == HomeEnvironmentVariable
                ? null
                : Environment.ExpandEnvironmentVariables(HomeEnvironmentVariable);

            if (!String.IsNullOrEmpty(HomePath))
                using (var wwwrootHandle = NativeMethods.CreateFile(Path.Combine(HomePath, SiteWwwroot),
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    IntPtr.Zero,
                    FileMode.Open,
                    FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS,
                    IntPtr.Zero))
                {
                    var wwwrootPath = GetNameFromHandle(wwwrootHandle);
                    wwwrootPath = Regex.Replace(wwwrootPath, Regex.Escape("\\" + SiteWwwroot), String.Empty,
                        RegexOptions.IgnoreCase);
                    UncPath = Regex.Replace(wwwrootPath, Regex.Escape(NetworkDevicePrefix), NetworkPrefix,
                        RegexOptions.IgnoreCase);
                }
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

        public Handle(ulong processId, ulong handle, ushort rawType)
        {
            ProcessId = (uint) processId;
            RawHandleValue = (uint) handle;
            RawType = rawType;

            using (var sourceProcessHandle =
                NativeMethods.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE, true,
                    ProcessId))
            {
                // To read info about a handle owned by another process we must duplicate it into ours
                if (!NativeMethods.DuplicateHandle(sourceProcessHandle,
                    (IntPtr)RawHandleValue,
                    NativeMethods.GetCurrentProcess(),
                    out _inProcessSafeHandle,
                    0,
                    false,
                    DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
                {
                    _inProcessSafeHandle = null;
                }
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
