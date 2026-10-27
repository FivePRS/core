using System;
using CitizenFX.Core.Native;

namespace FivePRS.Client
{
    public static class HudAnchor
    {
        private const float SafeZoneMargin = 0.05f;
        private const float MinimapHeightRatio = 5.674f;

        public static object Minimap()
        {
            var aspect = API.GetAspectRatio(false);
            var inset = SafeZoneMargin * Math.Abs(API.GetSafeZoneSize() - 1f) * 10f;
            var bottom = 1f - inset;

            return new
            {
                left = inset,
                top = bottom - 1f / MinimapHeightRatio,
                width = 1f / (4f * aspect),
                visible = !API.IsRadarHidden(),
            };
        }
    }
}
