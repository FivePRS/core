using CitizenFX.Core.Native;

namespace FivePRS.Server
{
    internal static class Resource
    {
        public static string Name { get; private set; } = "fiveprs";

        public static string Path { get; private set; } = string.Empty;

        public static void Capture()
        {
            Name = API.GetCurrentResourceName();
            Path = API.GetResourcePath(Name);
        }
    }
}
