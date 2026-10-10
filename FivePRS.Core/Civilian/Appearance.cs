using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FivePRS.Core.Civilian
{
    public sealed class HeadBlend
    {
        public int   ShapeFirst  { get; set; }
        public int   ShapeSecond { get; set; }
        public int   SkinFirst   { get; set; }
        public int   SkinSecond  { get; set; }
        public float ShapeMix    { get; set; } = 0.5f;
        public float SkinMix     { get; set; } = 0.5f;
    }

    public sealed class HeadOverlay
    {
        public int   Index       { get; set; }
        public int   Style       { get; set; } = -1;
        public float Opacity     { get; set; } = 1f;
        public int   Color       { get; set; }
        public int   SecondColor { get; set; }
    }

    public sealed class ClothingItem
    {
        public int Slot     { get; set; }
        public int Drawable { get; set; }
        public int Texture  { get; set; }
    }

    public sealed class CharacterAppearance
    {
        public string             Model         { get; set; } = AppearanceRules.MaleFreemode;
        public HeadBlend          HeadBlend     { get; set; } = new();
        public List<float>        FaceFeatures  { get; set; } = new();
        public List<HeadOverlay>  Overlays      { get; set; } = new();
        public int                HairColor     { get; set; }
        public int                HairHighlight { get; set; }
        public int                EyeColor      { get; set; }
        public List<ClothingItem> Components    { get; set; } = new();
        public List<ClothingItem> Props         { get; set; } = new();

        public bool IsFreemode => AppearanceRules.IsFreemode(Model);
    }

    public static class AppearanceRules
    {
        public const string MaleFreemode   = "mp_m_freemode_01";
        public const string FemaleFreemode = "mp_f_freemode_01";

        public const int ParentCount       = 46;
        public const int FaceFeatureCount  = 20;
        public const int OverlayCount      = 13;
        public const int ComponentCount    = 12;
        public const int MaxHairColor      = 63;
        public const int MaxEyeColor       = 31;
        public const int MaxDrawable       = 1023;
        public const int MaxTexture        = 63;
        public const int MaxOverlayStyle   = 254;
        public const int MaxSerializedSize = 16 * 1024;

        public static readonly int[] PropSlots = { 0, 1, 2, 6, 7 };

        private static readonly Regex ModelPattern = new("^[a-z0-9_]{1,40}$", RegexOptions.CultureInvariant);

        public static bool IsFreemode(string? model) =>
            string.Equals(model, MaleFreemode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(model, FemaleFreemode, StringComparison.OrdinalIgnoreCase);

        public static bool IsAllowedModel(string? model, IEnumerable<string> standardPeds) =>
            model is not null && ModelPattern.IsMatch(model) &&
            (IsFreemode(model) || standardPeds.Any(p => string.Equals(p, model, StringComparison.OrdinalIgnoreCase)));

        public static CharacterAppearance? Sanitize(CharacterAppearance? appearance, IEnumerable<string> standardPeds)
        {
            if (appearance is null) return null;

            var model = appearance.Model?.Trim().ToLowerInvariant();
            if (!IsAllowedModel(model, standardPeds)) return null;

            var blend = appearance.HeadBlend ?? new HeadBlend();

            return new CharacterAppearance
            {
                Model     = model!,
                HeadBlend = new HeadBlend
                {
                    ShapeFirst  = Clamp(blend.ShapeFirst, 0, ParentCount - 1),
                    ShapeSecond = Clamp(blend.ShapeSecond, 0, ParentCount - 1),
                    SkinFirst   = Clamp(blend.SkinFirst, 0, ParentCount - 1),
                    SkinSecond  = Clamp(blend.SkinSecond, 0, ParentCount - 1),
                    ShapeMix    = Clamp(blend.ShapeMix, 0f, 1f),
                    SkinMix     = Clamp(blend.SkinMix, 0f, 1f),
                },
                FaceFeatures = (appearance.FaceFeatures ?? new List<float>())
                    .Take(FaceFeatureCount)
                    .Select(f => Clamp(f, -1f, 1f))
                    .ToList(),
                Overlays = (appearance.Overlays ?? new List<HeadOverlay>())
                    .Where(o => o is not null && o.Index >= 0 && o.Index < OverlayCount)
                    .GroupBy(o => o.Index)
                    .Select(g => g.Last())
                    .Select(o => new HeadOverlay
                    {
                        Index       = o.Index,
                        Style       = Clamp(o.Style, -1, MaxOverlayStyle),
                        Opacity     = Clamp(o.Opacity, 0f, 1f),
                        Color       = Clamp(o.Color, 0, MaxHairColor),
                        SecondColor = Clamp(o.SecondColor, 0, MaxHairColor),
                    })
                    .OrderBy(o => o.Index)
                    .ToList(),
                HairColor     = Clamp(appearance.HairColor, 0, MaxHairColor),
                HairHighlight = Clamp(appearance.HairHighlight, 0, MaxHairColor),
                EyeColor      = Clamp(appearance.EyeColor, 0, MaxEyeColor),
                Components    = Clothing(appearance.Components, slot => slot >= 0 && slot < ComponentCount, 0),
                Props         = Clothing(appearance.Props, slot => PropSlots.Contains(slot), -1),
            };
        }

        private static List<ClothingItem> Clothing(IEnumerable<ClothingItem>? items, Func<int, bool> validSlot, int minimum) =>
            (items ?? Enumerable.Empty<ClothingItem>())
                .Where(i => i is not null && validSlot(i.Slot))
                .GroupBy(i => i.Slot)
                .Select(g => g.Last())
                .Select(i => new ClothingItem
                {
                    Slot     = i.Slot,
                    Drawable = Clamp(i.Drawable, minimum, MaxDrawable),
                    Texture  = Clamp(i.Texture, minimum, MaxTexture),
                })
                .OrderBy(i => i.Slot)
                .ToList();

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static float Clamp(float value, float min, float max) =>
            float.IsNaN(value) ? min : Math.Max(min, Math.Min(max, value));
    }
}
