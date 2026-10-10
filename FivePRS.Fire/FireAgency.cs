using FivePRS.Client.Agency;
using FivePRS.Client.Gear;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Fire
{
    public class FireAgency : DepartmentAgency
    {
        public override Department Department => Department.Fire;

        public override string AgencyName => "Fire Department";

        protected override DepartmentGear Gear { get; } = new(Department.Fire, tier => tier switch
        {
            LoadoutTier.Command => "Captain",
            LoadoutTier.Senior  => "Engineer",
            LoadoutTier.Officer => "Firefighter",
            _                   => "Probationary Firefighter",
        }, "Turnout gear");

        protected override string EquipmentNoun => "turnout gear, equipment and fire engine";
    }
}
