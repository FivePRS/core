using CitizenFX.Core.Native;

namespace FivePRS.Client
{
    public static class Binds
    {
        public static string Menu    => For("fiveprs");
        public static string Profile => For("er_profile");
        public static string Accept  => For("er_accept");
        public static string Decline => For("er_decline");
        public static string EndCall => For("er_end_callout");
        public static string Cuff    => For("er_cuff");
        public static string Uncuff  => For("er_uncuff");
        public static string Escort  => For("er_escort");

        public static string For(string command) =>
            $"~INPUT_{(uint)API.GetHashKey(command) | 0x80000000:X8}~";
    }
}
