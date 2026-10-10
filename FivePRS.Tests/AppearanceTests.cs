using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Server.Civilian;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public class AppearanceRulesTests
    {
        private static readonly string[] Standard = { "a_m_y_skater_01" };

        [Fact]
        public void Sanitize_ClampsValuesAndDropsInvalidEntries()
        {
            var result = AppearanceRules.Sanitize(new CharacterAppearance
            {
                Model        = "MP_F_FREEMODE_01",
                HeadBlend    = new HeadBlend { ShapeFirst = 99, SkinSecond = -3, ShapeMix = 4f, SkinMix = float.NaN },
                FaceFeatures = Enumerable.Repeat(3f, 30).ToList(),
                Overlays     = new List<HeadOverlay>
                {
                    new() { Index = 2, Style = 500, Opacity = 2f, Color = 99 },
                    new() { Index = 40, Style = 1 },
                },
                EyeColor   = 90,
                Components = new List<ClothingItem> { new() { Slot = 11, Drawable = 5, Texture = 2 }, new() { Slot = 12, Drawable = 1 } },
                Props      = new List<ClothingItem> { new() { Slot = 0, Drawable = -5, Texture = -9 }, new() { Slot = 3, Drawable = 1 } },
            }, Standard);

            Assert.NotNull(result);
            Assert.Equal(AppearanceRules.FemaleFreemode, result!.Model);
            Assert.Equal(45, result.HeadBlend.ShapeFirst);
            Assert.Equal(0, result.HeadBlend.SkinSecond);
            Assert.Equal(1f, result.HeadBlend.ShapeMix);
            Assert.Equal(0f, result.HeadBlend.SkinMix);
            Assert.Equal(AppearanceRules.FaceFeatureCount, result.FaceFeatures.Count);
            Assert.All(result.FaceFeatures, f => Assert.Equal(1f, f));
            var overlay = Assert.Single(result.Overlays);
            Assert.Equal((2, 254, 1f, 63), (overlay.Index, overlay.Style, overlay.Opacity, overlay.Color));
            Assert.Equal(AppearanceRules.MaxEyeColor, result.EyeColor);
            Assert.Equal(11, Assert.Single(result.Components).Slot);
            var prop = Assert.Single(result.Props);
            Assert.Equal((0, -1, -1), (prop.Slot, prop.Drawable, prop.Texture));
        }

        [Theory]
        [InlineData("a_m_y_skater_01", true)]
        [InlineData("mp_m_freemode_01", true)]
        [InlineData("a_c_chop", false)]
        [InlineData("bad model;", false)]
        public void Sanitize_OnlyAllowsFreemodeAndListedPeds(string model, bool allowed)
        {
            Assert.Equal(allowed, AppearanceRules.Sanitize(new CharacterAppearance { Model = model }, Standard) is not null);
        }
    }

    [Collection(TestDatabase.Collection)]
    public sealed class AppearanceServiceTests : IAsyncLifetime
    {
        private const string Owner = "license:owner";

        private TestDatabase _database = null!;
        private CivilianService _civilians = null!;
        private AppearanceService _service = null!;

        public async Task InitializeAsync()
        {
            _database  = await TestDatabase.CreateAsync();
            _civilians = new CivilianService(_database.Store, () => new ResourceSettings(), () => new LicensesConfig(), () => new DateTime(2026, 10, 9));
            _service   = new AppearanceService(_database.Store, _database.Appearances, () => new CreatorSettings());
            await _civilians.CreateCharacterAsync(Owner, "Jane", "Doe", "1990-01-01", "female");
        }

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        private async Task<int> CharacterIdAsync() => (await _civilians.GetActiveCharacterAsync(Owner))!.Id;

        [Fact]
        public async Task Save_StoresSanitizedAppearanceAndOverwrites()
        {
            var id = await CharacterIdAsync();

            Assert.Null(await _service.SaveAsync(Owner, id, JsonConvert.SerializeObject(new CharacterAppearance { Model = "mp_f_freemode_01", EyeColor = 99 })));
            Assert.Null(await _service.SaveAsync(Owner, id, JsonConvert.SerializeObject(new CharacterAppearance { Model = "a_f_y_hipster_01", EyeColor = 3 })));

            var stored = JsonConvert.DeserializeObject<CharacterAppearance>((await _service.GetAsync(id))!)!;
            Assert.Equal("a_f_y_hipster_01", stored.Model);
            Assert.Equal(3, stored.EyeColor);
        }

        [Fact]
        public async Task Save_RejectsOtherOwnersInvalidJsonAndUnknownModels()
        {
            var id = await CharacterIdAsync();

            Assert.NotNull(await _service.SaveAsync("license:other", id, JsonConvert.SerializeObject(new CharacterAppearance())));
            Assert.NotNull(await _service.SaveAsync(Owner, id, "{not json"));
            Assert.NotNull(await _service.SaveAsync(Owner, id, JsonConvert.SerializeObject(new CharacterAppearance { Model = "a_c_chop" })));
            Assert.Null(await _service.GetAsync(id));
        }

        [Fact]
        public async Task Delete_RemovesAppearance()
        {
            var id = await CharacterIdAsync();
            await _service.SaveAsync(Owner, id, JsonConvert.SerializeObject(new CharacterAppearance()));

            await _service.DeleteAsync(id);

            Assert.Null(await _service.GetAsync(id));
        }
    }
}
