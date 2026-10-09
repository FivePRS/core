using CitizenFX.Core.Native;

namespace FivePRS.Client
{
    public static class NuiFocus
    {
        public static void Take()
        {
            API.SetNuiFocus(false, false);
            API.SetNuiFocus(true, true);
        }

        public static void Release() => API.SetNuiFocus(false, false);

        public static void EnsureTaken()
        {
            if (!API.IsNuiFocused())
                Take();
        }
    }
}
