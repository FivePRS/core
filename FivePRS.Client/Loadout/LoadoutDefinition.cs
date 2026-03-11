using System.Collections.Generic;

namespace FivePRS.Client.Loadout
{
    /// <summary>
    /// Describes a complete officer setup: weapons, ped components (uniform), and ped props (hat etc.).
    /// Instances are defined in department Config classes and passed to <see cref="LoadoutManager"/>.
    /// </summary>
    public sealed class LoadoutDefinition
    {
        public string Name { get; init; } = "Default";

        public IReadOnlyList<WeaponEntry> Weapons { get; init; } = new List<WeaponEntry>();

        public IReadOnlyList<ComponentEntry> Components { get; init; } = new List<ComponentEntry>();

        public IReadOnlyList<PropEntry> Props { get; init; } = new List<PropEntry>();
    }

    /// <summary>A single weapon to give the player, with initial ammo.</summary>
    public sealed class WeaponEntry
    {
        public uint Hash { get; init; }

        public int Ammo { get; init; }

        public bool SetAsCurrent { get; init; }
    }

    /// <summary>
    /// One drawable/texture pair for a ped component slot.
    ///
    /// GTA V component IDs:
    ///   0=Head  1=Mask  2=Hair  3=Torso  4=Legs  5=Bag  6=Feet
    ///   7=Accessories  8=Undershirt  9=Armor  10=Decals  11=Jacket
    /// </summary>
    public sealed class ComponentEntry
    {
        public int ComponentId { get; init; }
        public int DrawableId  { get; init; }
        public int TextureId   { get; init; }
    }

    /// <summary>
    /// One drawable/texture pair for a ped prop slot.
    ///
    /// GTA V prop IDs:
    ///   0=Hat  1=Glasses  2=Ear  6=Watch  7=Bracelet
    /// To clear a prop slot set DrawableId = -1.
    /// </summary>
    public sealed class PropEntry
    {
        public int PropId      { get; init; }
        public int DrawableId  { get; init; }
        public int TextureId   { get; init; }
    }
}
