using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SQLitePCL;

namespace FivePRS.Server.Database
{
    internal static class SqliteNativeLoader
    {
        private const string LibraryName = "e_sqlite3";

        private static bool _loaded;

        public static string Load(string directory)
        {
            var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var path = Path.Combine(directory, windows ? "e_sqlite3.dll" : "libe_sqlite3.so");
            if (_loaded) return path;

            if (!File.Exists(path))
                throw new FileNotFoundException("Native SQLite library is missing from the resource.", path);

            IGetFunctionPointer symbols;
            try
            {
                symbols = OpenLibrary(path);
            }
            catch (Exception ex) when (!(ex is DllNotFoundException))
            {
                throw new NotSupportedException(
                    "This server doesn't allow resources to load native libraries (GTA V Enhanced servers block them), so SQLite can't run. " +
                    "Use MySQL instead: set fiveprs_db_type \"mysql\" and fiveprs_db_connection in server.cfg.", ex);
            }

            SQLite3Provider_dynamic_cdecl.Setup(LibraryName, symbols);
            raw.SetProvider(new SQLite3Provider_dynamic_cdecl());
            raw.FreezeProvider(true);

            _loaded = true;
            return path;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IGetFunctionPointer OpenLibrary(string path) => new Symbols(FivePRS.Native.NativeLibrary.Open(path));

        private sealed class Symbols : IGetFunctionPointer
        {
            private readonly FivePRS.Native.NativeLibrary _library;

            public Symbols(FivePRS.Native.NativeLibrary library) => _library = library;

            public IntPtr GetFunctionPointer(string name) => _library.Symbol(name);
        }
    }
}
