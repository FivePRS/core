using System.Collections.Generic;

namespace FivePRS.Client.Loadout
{
    public sealed class LoadoutDefinition
    {
        public string Name { get; set; } = "Default";

        public IReadOnlyList<WeaponEntry> Weapons { get; set; } = new List<WeaponEntry>();

        public IReadOnlyList<ComponentEntry> Components { get; set; } = new List<ComponentEntry>();

        public IReadOnlyList<PropEntry> Props { get; set; } = new List<PropEntry>();
    }

    public sealed class WeaponEntry
    {
        public uint Hash { get; set; }

        public int Ammo { get; set; }

        public bool SetAsCurrent { get; set; }
    }

    public sealed class ComponentEntry
    {
        public int ComponentId { get; set; }
        public int DrawableId  { get; set; }
        public int TextureId   { get; set; }
    }

    public sealed class PropEntry
    {
        public int PropId      { get; set; }
        public int DrawableId  { get; set; }
        public int TextureId   { get; set; }
    }
}
