using System.Collections.Generic;
using FivePRS.Core.Civilian;

namespace FivePRS.Core.Police
{
    public static class PlateFlag
    {
        public const string Stolen = "stolen";
        public const string Warrant = "warrant";
        public const string Unregistered = "unregistered";
        public const string Suspended = "suspended";
    }

    public sealed class PlateCheck
    {
        public string Plate { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Owner { get; set; }
        public List<string> Flags { get; set; } = new();
    }

    public static class PlateFlags
    {
        public static PlateCheck FromRecord(string plate, VehicleInfo vehicle, LookupRecord? owner)
        {
            var check = new PlateCheck { Plate = plate, Model = vehicle.Model, Owner = owner is null ? null : $"{owner.Character.FirstName} {owner.Character.LastName}" };
            if (vehicle.Status == VehicleStatus.Stolen) check.Flags.Add(PlateFlag.Stolen);
            if (owner is null) return check;

            if (owner.Character.HasActiveWarrant) check.Flags.Add(PlateFlag.Warrant);
            foreach (var license in owner.Licenses)
            {
                if (license.Type == StreetProfiles.DriverLicenseType && (license.Status == LicenseStatus.Suspended || license.Status == LicenseStatus.Revoked))
                    check.Flags.Add(PlateFlag.Suspended);
            }
            return check;
        }

        public static PlateCheck FromProfile(string plate)
        {
            var vehicle = StreetProfiles.Vehicle(plate);
            var check = new PlateCheck { Plate = vehicle.Plate };
            if (!vehicle.Registered)
            {
                check.Flags.Add(PlateFlag.Unregistered);
                return check;
            }

            if (vehicle.Stolen) check.Flags.Add(PlateFlag.Stolen);
            if (vehicle.DriverIsOwner) return check;

            var owner = StreetProfiles.Person(vehicle.OwnerSeed, vehicle.OwnerFemale);
            if (owner.Warrant is not null) check.Flags.Add(PlateFlag.Warrant);
            if (owner.DriverLicense == StreetLicense.Suspended || owner.DriverLicense == StreetLicense.Revoked) check.Flags.Add(PlateFlag.Suspended);
            return check;
        }
    }
}
