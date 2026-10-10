using CitizenFX.Core.Native;
using FivePRS.Core.Text;

namespace FivePRS.Client
{
    public static class Binds
    {
        public static string Menu     => For(KeyCommands.Menu);
        public static string Interact => For(KeyCommands.Interact);
        public static string Accept   => For(KeyCommands.Accept);
        public static string Decline  => For(KeyCommands.Decline);
        public static string EndCall  => For(KeyCommands.EndCall);
        public static string Cuff     => For(KeyCommands.Cuff);
        public static string Uncuff   => For(KeyCommands.Uncuff);
        public static string Escort   => For(KeyCommands.Escort);

        public static string For(string command) =>
            $"~INPUT_{(uint)API.GetHashKey(command) | 0x80000000:X8}~";
    }
}
