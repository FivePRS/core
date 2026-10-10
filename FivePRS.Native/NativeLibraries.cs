using System;
using System.Runtime.InteropServices;

namespace FivePRS.Native
{
    public sealed class NativeLibrary
    {
        private const int RtldNow = 2;
        private const int RtldGlobal = 0x100;

        private readonly IntPtr _handle;
        private readonly Func<IntPtr, string, IntPtr> _symbol;

        private NativeLibrary(IntPtr handle, Func<IntPtr, string, IntPtr> symbol)
        {
            _handle = handle;
            _symbol = symbol;
        }

        public IntPtr Symbol(string name) => _symbol(_handle, name);

        public static NativeLibrary Open(string path) =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OpenWindows(path) : OpenUnix(path);

        private static NativeLibrary OpenWindows(string path)
        {
            var handle = Windows.LoadLibraryW(path);
            if (handle == IntPtr.Zero)
                throw new DllNotFoundException($"Could not load {path} (Win32 error {Marshal.GetLastWin32Error()}).");
            return new NativeLibrary(handle, Windows.GetProcAddress);
        }

        private static NativeLibrary OpenUnix(string path)
        {
            try
            {
                return OpenUnix(path, Internal.dlopen(path, RtldNow | RtldGlobal), Internal.dlerror, Internal.dlsym);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                return OpenUnix(path, Libdl.dlopen(path, RtldNow | RtldGlobal), Libdl.dlerror, Libdl.dlsym);
            }
        }

        private static NativeLibrary OpenUnix(string path, IntPtr handle, Func<IntPtr> error, Func<IntPtr, string, IntPtr> symbol)
        {
            if (handle != IntPtr.Zero) return new NativeLibrary(handle, symbol);

            var message = error();
            throw new DllNotFoundException($"Could not load {path} ({(message == IntPtr.Zero ? "unknown error" : Marshal.PtrToStringAnsi(message))}).");
        }

        private static class Windows
        {
            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr LoadLibraryW(string path);

            [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
            public static extern IntPtr GetProcAddress(IntPtr module, string name);
        }

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
