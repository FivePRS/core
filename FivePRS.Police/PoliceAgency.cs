using FivePRS.Client.Agency;
using FivePRS.Client.Gear;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Police
{
    public class PoliceAgency : DepartmentAgency
    {
        public override Department Department => Department.Police;

        public override string AgencyName => "Police Department";

        protected override DepartmentGear Gear { get; } = new(Department.Police, tier => tier switch
        {
            LoadoutTier.Command => "Command",
            LoadoutTier.Senior  => "Senior Officer",
            LoadoutTier.Officer => "Officer",
            _                   => "Recruit",
        }, "Patrol uniform");

        protected override string EquipmentNoun => "uniform, weapons and vehicle";
    }
}
