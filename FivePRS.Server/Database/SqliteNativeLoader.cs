using System;
using System.IO;
using System.Runtime.InteropServices;
using SQLitePCL;

namespace FivePRS.Server.Database
{
    internal static class SqliteNativeLoader
    {
        private const string LibraryName = "e_sqlite3";
        private const int RtldNow = 2;
        private const int RtldGlobal = 0x100;

        private static bool _loaded;

        public static string Load(string directory)
        {
            var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var path = Path.Combine(directory, windows ? "e_sqlite3.dll" : "libe_sqlite3.so");
            if (_loaded) return path;

            if (!File.Exists(path))
                throw new FileNotFoundException("Native SQLite library is missing from the resource.", path);

            IGetFunctionPointer symbols = windows ? WindowsLibrary.Open(path) : UnixLibrary.Open(path);

            SQLite3Provider_dynamic_cdecl.Setup(LibraryName, symbols);
            raw.SetProvider(new SQLite3Provider_dynamic_cdecl());
            raw.FreezeProvider(true);

            _loaded = true;
            return path;
        }

        private sealed class WindowsLibrary : IGetFunctionPointer
        {
            private readonly IntPtr _handle;

            private WindowsLibrary(IntPtr handle) => _handle = handle;

            public static WindowsLibrary Open(string path)
            {
                var handle = LoadLibraryW(path);
                if (handle == IntPtr.Zero)
                    throw new DllNotFoundException($"Could not load native SQLite from {path} (Win32 error {Marshal.GetLastWin32Error()}).");
                return new WindowsLibrary(handle);
            }

            public IntPtr GetFunctionPointer(string name) => GetProcAddress(_handle, name);

            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern IntPtr LoadLibraryW(string path);

            [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
            private static extern IntPtr GetProcAddress(IntPtr module, string name);
        }

        private sealed class UnixLibrary : IGetFunctionPointer
        {
            private readonly IntPtr _handle;
            private readonly bool _useLibdl;

            private UnixLibrary(IntPtr handle, bool useLibdl)
            {
                _handle = handle;
                _useLibdl = useLibdl;
            }

            public static UnixLibrary Open(string path)
            {
                IntPtr handle;
                bool useLibdl;
                try
                {
                    handle = Internal.dlopen(path, RtldNow | RtldGlobal);
                    useLibdl = false;
                }
                catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
                {
                    handle = Libdl.dlopen(path, RtldNow | RtldGlobal);
                    useLibdl = true;
                }

                if (handle == IntPtr.Zero)
                {
                    var error = useLibdl ? Libdl.dlerror() : Internal.dlerror();
                    var message = error == IntPtr.Zero ? "unknown error" : Marshal.PtrToStringAnsi(error);
                    throw new DllNotFoundException($"Could not load native SQLite from {path} ({message}).");
                }

                return new UnixLibrary(handle, useLibdl);
            }

            public IntPtr GetFunctionPointer(string name) =>
                _useLibdl ? Libdl.dlsym(_handle, name) : Internal.dlsym(_handle, name);

            private static class Internal
            {
                [DllImport("__Internal")]
                public static extern IntPtr dlopen(string path, int flags);

                [DllImport("__Internal")]
                public static extern IntPtr dlsym(IntPtr handle, string name);

                [DllImport("__Internal")]
                public static extern IntPtr dlerror();
            }

            private static class Libdl
            {
                [DllImport("libdl.so.2")]
                public static extern IntPtr dlopen(string path, int flags);

                [DllImport("libdl.so.2")]
                public static extern IntPtr dlsym(IntPtr handle, string name);

                [DllImport("libdl.so.2")]
                public static extern IntPtr dlerror();
            }
        }
    }
}
