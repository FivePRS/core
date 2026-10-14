using FivePRS.Core.Models;

namespace FivePRS.Core.Config
{
    public static class PlayerRanks
    {
        public static RankLadder Ladder(this PlayerData player) => ConfigManager.RanksFor(player.Department);

        public static string RankName(this PlayerData player) => player.Ladder().NameFor(player.Rank, player.Agency);
    }
}
