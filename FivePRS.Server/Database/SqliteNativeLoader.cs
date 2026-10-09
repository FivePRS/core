using System;
using System.IO;
using System.Runtime.InteropServices;
using CitizenFX.Core;

namespace FivePRS.Server.Database
{
    internal static class SqliteNativeLoader
    {
        private const int RtldNow = 2;
        private const int RtldGlobal = 0x100;

        private static bool _loaded;

        [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("libc", EntryPoint = "dlopen")]
        private static extern IntPtr DlopenLibc(string path, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr DlopenLibdl(string path, int flags);

        public static void Load(string directory)
        {
            if (_loaded) return;

            var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var path = Path.Combine(directory, windows ? "e_sqlite3.dll" : "libe_sqlite3.so");

            if (!File.Exists(path))
                throw new FileNotFoundException("Native SQLite library is missing from the resource.", path);

            var handle = windows ? LoadLibrary(path) : Dlopen(path);
            if (handle == IntPtr.Zero)
            {
                var detail = windows ? $"Win32 error {Marshal.GetLastWin32Error()}" : "dlopen returned null";
                throw new DllNotFoundException($"Could not load native SQLite from {path} ({detail}).");
            }

            _loaded = true;
            Debug.WriteLine($"[FivePRS] Native SQLite loaded from {path}.");
        }

        private static IntPtr Dlopen(string path)
        {
            try
            {
                return DlopenLibc(path, RtldNow | RtldGlobal);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                return DlopenLibdl(path, RtldNow | RtldGlobal);
            }
        }
    }
}
