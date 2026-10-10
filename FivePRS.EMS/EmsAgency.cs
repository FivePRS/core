using FivePRS.Client.Agency;
using FivePRS.Client.Gear;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.EMS
{
    public class EmsAgency : DepartmentAgency
    {
        public override Department Department => Department.EMS;

        public override string AgencyName => "Emergency Medical Services";

        protected override DepartmentGear Gear { get; } = new(Department.EMS, tier => tier switch
        {
            LoadoutTier.Command => "Supervisor",
            LoadoutTier.Senior  => "Paramedic",
            LoadoutTier.Officer => "EMT",
            _                   => "EMT Trainee",
        }, "Medic uniform");

        protected override string EquipmentNoun => "uniform, equipment and ambulance";
    }
}
