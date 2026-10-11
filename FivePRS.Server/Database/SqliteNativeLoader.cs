using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using SQLitePCL;

namespace FivePRS.Server.Database
{
    internal static class SqliteNativeLoader
    {
        private const string LibraryName = "e_sqlite3";
        private const string NativeAssembly = "FivePRS.Native";
        private const string NativeType = "FivePRS.Native.NativeLibrary";

        private static bool _loaded;

        public static string Load(string directory)
        {
            var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var path = Path.Combine(directory, windows ? "e_sqlite3.dll" : "libe_sqlite3.so");
            if (_loaded) return path;

            if (!File.Exists(path))
                throw new FileNotFoundException("Native SQLite library is missing from the resource.", path);

            var symbols = OpenLibrary(path);

            SQLite3Provider_dynamic_cdecl.Setup(LibraryName, symbols);
            raw.SetProvider(new SQLite3Provider_dynamic_cdecl());
            raw.FreezeProvider(true);

            _loaded = true;
            return path;
        }

        private static IGetFunctionPointer OpenLibrary(string path)
        {
            Type library;
            try
            {
                library = Assembly.Load(NativeAssembly).GetType(NativeType, true)!;
            }
            catch (Exception ex)
            {
                throw new NotSupportedException(
                    "This server doesn't allow resources to load native libraries (GTA V Enhanced servers block them), so SQLite can't run. " +
                    "Use MySQL or MariaDB instead: set fiveprs_db_type \"mysql\" and fiveprs_db_connection in server.cfg.", ex);
            }

            var open   = library.GetMethod("Open", BindingFlags.Public | BindingFlags.Static)!;
            var symbol = library.GetMethod("Symbol", BindingFlags.Public | BindingFlags.Instance)!;

            object handle;
            try
            {
                handle = open.Invoke(null, new object[] { path })!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }

            return new Symbols(name => (IntPtr)symbol.Invoke(handle, new object[] { name })!);
        }

        private sealed class Symbols : IGetFunctionPointer
        {
            private readonly Func<string, IntPtr> _lookup;

            public Symbols(Func<string, IntPtr> lookup) => _lookup = lookup;

            public IntPtr GetFunctionPointer(string name) => _lookup(name);
        }
    }
}
