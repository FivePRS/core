using System;
using System.Globalization;
using FivePRS.Core.Civilian;

namespace FivePRS.Core.Police
{
    public enum StreetLicense
    {
        Valid,
        None,
        Suspended,
        Revoked,
    }

    public sealed class StreetPerson
    {
        public int Seed { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public StreetLicense DriverLicense { get; set; }
        public string? Warrant { get; set; }
        public string? Contraband { get; set; }
        public bool WillFlee { get; set; }
        public string? Medical { get; set; }

        public string FullName => $"{FirstName} {LastName}";
    }

    public sealed class StreetVehicle
    {
        public string Plate { get; set; } = string.Empty;
        public bool Registered { get; set; }
        public bool Stolen { get; set; }
        public bool DriverIsOwner { get; set; }
        public int OwnerSeed { get; set; }
        public bool OwnerFemale { get; set; }
    }

    public static class StreetProfiles
    {
        public const string NpcOwner = "npc";
        public const string DriverLicenseType = "driver";

        private static readonly string[] MaleNames =
        {
            "James", "Michael", "Robert", "David", "William", "Richard", "Joseph", "Thomas", "Daniel", "Matthew",
            "Anthony", "Mark", "Steven", "Paul", "Andrew", "Joshua", "Kevin", "Brian", "Jason", "Ryan",
            "Eric", "Jacob", "Tyler", "Aaron", "Luis", "Carlos", "Marcus", "Trevor", "Dwayne", "Hector",
        };

        private static readonly string[] FemaleNames =
        {
            "Mary", "Jennifer", "Linda", "Elizabeth", "Susan", "Jessica", "Sarah", "Karen", "Lisa", "Nancy",
            "Ashley", "Emily", "Michelle", "Amanda", "Melissa", "Stephanie", "Rebecca", "Laura", "Amy", "Angela",
            "Brittany", "Megan", "Rachel", "Maria", "Sofia", "Tanisha", "Monica", "Denise", "Crystal", "Vanessa",
        };

        private static readonly string[] LastNames =
        {
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
            "Hernandez", "Lopez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee",
            "Thompson", "White", "Harris", "Clark", "Lewis", "Robinson", "Walker", "Young", "Allen", "King",
            "Wright", "Scott", "Torres", "Hill", "Flores", "Green", "Adams", "Nelson", "Baker", "Hall",
            "Rivera", "Campbell", "Mitchell", "Carter", "Roberts", "Reyes", "Cruz", "Morales", "Ortiz", "Price",
        };

        private static readonly string[] Warrants =
        {
            "Failure to appear in court", "Unpaid traffic fines", "Probation violation", "Petty theft",
            "Simple assault", "Possession of a controlled substance", "Driving while suspended",
        };

        private static readonly string[] Conditions =
        {
            "Type 1 diabetes, insulin dependent", "Severe penicillin allergy", "Asthma, carries an inhaler",
            "Epilepsy, history of seizures", "Heart condition, takes blood thinners", "Severe peanut allergy, carries an EpiPen",
            "High blood pressure", "COPD", "Previous heart attack", "Hemophilia",
        };

        private static readonly string[] Contraband =
        {
            "A small bag of cannabis", "An open container of alcohol", "Drug paraphernalia",
            "An unregistered pistol", "Several stolen credit cards", "A switchblade",
        };

        public static StreetPerson Person(int seed, bool female)
        {
            var rng = new Random(seed);
            var names = female ? FemaleNames : MaleNames;

            var person = new StreetPerson
            {
                Seed      = seed,
                FirstName = names[rng.Next(names.Length)],
                LastName  = LastNames[rng.Next(LastNames.Length)],
                Gender    = female ? "Female" : "Male",
            };

            var year  = 1950 + rng.Next(57);
            var month = 1 + rng.Next(12);
            var day   = 1 + rng.Next(DateTime.DaysInMonth(year, month));
            person.DateOfBirth = new DateTime(year, month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var license = rng.Next(100);
            person.DriverLicense = license < 82 ? StreetLicense.Valid
                : license < 88 ? StreetLicense.None
                : license < 96 ? StreetLicense.Suspended
                : StreetLicense.Revoked;

            if (rng.Next(100) < 7) person.Warrant = Warrants[rng.Next(Warrants.Length)];
            if (rng.Next(100) < 15) person.Contraband = Contraband[rng.Next(Contraband.Length)];
            person.WillFlee = person.Warrant is not null && rng.Next(100) < 35;
            if (rng.Next(100) < 20) person.Medical = Conditions[rng.Next(Conditions.Length)];

            return person;
        }

        public static StreetVehicle Vehicle(string plate)
        {
            var normalized = (plate ?? string.Empty).Trim().ToUpperInvariant();
            var rng = new Random(StableHash(normalized));

            var vehicle = new StreetVehicle
            {
                Plate         = normalized,
                Registered    = rng.Next(100) < 94,
                DriverIsOwner = rng.Next(100) < 80,
                OwnerSeed     = rng.Next(),
                OwnerFemale   = rng.Next(2) == 0,
            };
            vehicle.Stolen = vehicle.Registered && rng.Next(100) < 4;
            if (vehicle.Stolen) vehicle.DriverIsOwner = false;

            return vehicle;
        }

        public static LicenseStatus? ToLicenseStatus(StreetLicense license) => license switch
        {
            StreetLicense.Valid     => LicenseStatus.Valid,
            StreetLicense.Suspended => LicenseStatus.Suspended,
            StreetLicense.Revoked   => LicenseStatus.Revoked,
            _                       => null,
        };

        public static int StableHash(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var c in text)
                    hash = (hash ^ c) * 16777619;
                return hash & 0x7FFFFFFF;
            }
        }
    }
}
