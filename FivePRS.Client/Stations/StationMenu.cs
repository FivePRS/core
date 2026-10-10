using System;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Client.Agency;
using FivePRS.Client.Loadout;
using FivePRS.Client.Menu;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;

namespace FivePRS.Client.Stations
{
    public static class StationMenu
    {
        private const float StayRadius = 3f;
        private const float ReturnRadius = 60f;

        private static readonly StationPoint[] GearPoints = { StationPoint.Armory, StationPoint.Locker, StationPoint.Garage };

        public static bool CanUse(StationDef station)
        {
            var player = ClientBrain.LocalPlayerData;
            return BaseAgency.Active is not null && player.IsOnDuty && station.Serves(player.Department, player.Agency);
        }

        public static void Open(StationDef station, StationPoint point)
        {
            if (!CanUse(station))
            {
                if (point == StationPoint.Duty)
                    BaseScript.TriggerEvent(EventNames.LocalOpenMenu, "duty");
                else
                    Notify(station.Name, "Go on duty with this station's department to use it.");
                return;
            }

            var anchor = StationService.PositionOf(station, point);

            CompactMenu.Open(() => PageFor(station, point), () =>
            {
                var ped = Game.PlayerPed;
                return ped.Exists() && !ped.IsDead && !ped.IsInVehicle() &&
                       Vector3.Distance(ped.Position, anchor) <= StayRadius && CanUse(station);
            }, IconFor(station.Department));
        }

        private static string IconFor(Department department)
        {
            var icons = ConfigManager.Settings.Branding.Departments;
            return department switch
            {
                Department.EMS  => icons.Ems,
                Department.Fire => icons.Fire,
                _               => icons.Police,
            };
        }

        private static MenuPage PageFor(StationDef station, StationPoint point) => point switch
        {
            StationPoint.Armory => Armory(station),
            StationPoint.Locker => Locker(station),
            StationPoint.Garage => Garage(station),
            _                   => Root(station),
        };

        private static MenuPage Root(StationDef station)
        {
            var page = new MenuPage { Title = station.Name, Subtitle = ClientBrain.LocalPlayerData.Callsign };

            foreach (var point in GearPoints.Where(p => !StationService.HasOwnPoint(station, p)))
                page.Items.Add(new MenuItem { Label = point.ToString(), Submenu = () => PageFor(station, point) });

            page.Items.Add(new MenuItem
            {
                Label  = "Duty menu",
                Select = () =>
                {
                    CompactMenu.Close();
                    BaseScript.TriggerEvent(EventNames.LocalOpenMenu, "duty");
                    return Done;
                },
            });

            return page;
        }

        private static MenuPage Armory(StationDef station)
        {
            var catalog = Catalog();
            var page    = new MenuPage { Title = "Armory", Subtitle = station.Name };

            if (catalog.Kits.Count > 0)
                page.Items.Add(new MenuItem { Label = "Kits", Submenu = () => Kits(catalog) });

            if (catalog.Weapons.Count > 0)
                page.Items.Add(new MenuItem { Label = "Weapons", Submenu = () => Weapons(station) });

            page.Items.Add(Action("Body armour", null, () =>
            {
                LoadoutManager.GiveArmour();
                Notify("Armory", "Body armour equipped.");
            }));

            page.Items.Add(Action("Refill ammo", null, () =>
            {
                var refilled = LoadoutManager.RefillAmmo();
                Notify("Armory", refilled > 0 ? "Ammo refilled." : "Your ammo is already full.");
            }));

            page.Items.Add(Action("Return weapons", null, () =>
            {
                LoadoutManager.RemoveWeapons();
                Notify("Armory", "Weapons returned.");
            }));

            return page;
        }

        private static MenuPage Kits(StationCatalog catalog)
        {
            var rank = ClientBrain.LocalPlayerData.Rank;
            var page = new MenuPage { Title = "Kits", Subtitle = "Replaces your current weapons" };

            foreach (var kit in catalog.Kits)
            {
                var locked = rank < kit.MinRank;
                page.Items.Add(new MenuItem
                {
                    Label    = kit.Name,
                    Detail   = locked ? $"Rank {kit.MinRank}" : $"{kit.Weapons.Count} items",
                    Disabled = locked,
                    Select   = async () =>
                    {
                        await LoadoutManager.GiveWeaponsAsync(kit.Weapons, true);
                        Notify("Armory", $"{kit.Name} kit issued.");
                    },
                });
            }

            return page;
        }

        private static MenuPage Weapons(StationDef station)
        {
            var rank = ClientBrain.LocalPlayerData.Rank;
            var page = new MenuPage { Title = "Weapons", Subtitle = station.Name };

            foreach (var weapon in Catalog().Weapons)
            {
                var locked = rank < weapon.MinRank;
                page.Items.Add(new MenuItem
                {
                    Label    = weapon.Label,
                    Detail   = locked ? $"Rank {weapon.MinRank}" : LoadoutManager.HasWeapon(weapon.Weapon.Hash) ? "Issued" : null,
                    Disabled = locked,
                    Select   = () =>
                    {
                        LoadoutManager.GiveWeapon(weapon.Weapon);
                        Notify("Armory", $"{weapon.Label} issued.");
                        return Done;
                    },
                });
            }

            return page;
        }

        private static MenuPage Locker(StationDef station)
        {
            var rank = ClientBrain.LocalPlayerData.Rank;
            var page = new MenuPage { Title = "Locker", Subtitle = station.Name };

            foreach (var uniform in Catalog().Uniforms)
            {
                var locked = rank < uniform.MinRank;
                page.Items.Add(new MenuItem
                {
                    Label    = uniform.Name,
                    Detail   = locked ? $"Rank {uniform.MinRank}" : null,
                    Disabled = locked,
                    Select   = async () =>
                    {
                        await LoadoutManager.WearAsync(uniform.Outfit);
                        Notify("Locker", $"Changed into the {uniform.Name.ToLowerInvariant()}.");
                    },
                });
            }

            page.Items.Add(new MenuItem
            {
                Label    = "Civilian clothes",
                Disabled = !LoadoutManager.IsInUniform,
                Select   = async () =>
                {
                    await LoadoutManager.RestoreClothingAsync();
                    Notify("Locker", "Changed into your own clothes.");
                },
            });

            return page;
        }

        private static MenuPage Garage(StationDef station)
        {
            var agency  = BaseAgency.Active;
            var rank    = ClientBrain.LocalPlayerData.Rank;
            var current = agency?.Vehicles.Current;
            var page    = new MenuPage
            {
                Title    = "Garage",
                Subtitle = current is null ? station.Name : "Replaces your current vehicle",
            };

            foreach (var vehicle in Catalog().Vehicles)
            {
                var locked = rank < vehicle.MinRank;
                page.Items.Add(new MenuItem
                {
                    Label    = vehicle.Label,
                    Detail   = locked ? $"Rank {vehicle.MinRank}" : null,
                    Disabled = locked || agency is null,
                    Select   = async () =>
                    {
                        var spawned = await agency!.Vehicles.SpawnAsync(vehicle.Config, StationService.ParkingSpot(station), false);
                        Notify("Garage", spawned is not null
                            ? $"Your {vehicle.Label} is parked outside."
                            : "~r~The vehicle could not be spawned.");
                    },
                });
            }

            var garage = StationService.PositionOf(station, StationPoint.Garage);
            var nearby = current is not null && Vector3.Distance(current.Position, garage) <= ReturnRadius;
            page.Items.Add(new MenuItem
            {
                Label    = "Return vehicle",
                Detail   = current is null ? "None out" : nearby ? null : "Bring it back first",
                Disabled = !nearby,
                Select   = () =>
                {
                    agency!.Vehicles.Despawn();
                    Notify("Garage", "Vehicle returned.");
                    return Done;
                },
            });

            return page;
        }

        private static StationCatalog Catalog() =>
            BaseAgency.Active?.BuildCatalog(ClientBrain.LocalPlayerData) ?? new StationCatalog();

        private static MenuItem Action(string label, string? detail, Action action) => new()
        {
            Label  = label,
            Detail = detail,
            Select = () =>
            {
                action();
                return Done;
            },
        };

        private static Task Done => Task.FromResult(0);

        private static void Notify(string subject, string message) =>
            ClientBrain.ShowNotification(message, subject);
    }
}
