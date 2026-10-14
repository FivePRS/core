using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Stations;
using FivePRS.Client.VehicleSpawner;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Interfaces;
using FivePRS.Core.Logging;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client.Agency
{
    public abstract class BaseAgency : BaseScript, IAgency
    {
        private Logger? _log;

        protected Logger Log => _log ??= Logger.For(AgencyName);

        private static readonly HashSet<Department> LoadedDepartments = new();

        public abstract Department Department { get; }
        public abstract string AgencyName { get; }

        public bool IsActive { get; private set; }

        public static BaseAgency? Active { get; private set; }

        public PatrolVehicleSpawner Vehicles { get; } = new();

        public virtual StationCatalog? BuildCatalog(PlayerData player) => null;

        protected static PlayerData CurrentPlayer => ClientBrain.LocalPlayerData;

        protected string DisplayName =>
            ConfigManager.Territories.FindAgency(CurrentPlayer.Agency)?.Name ?? AgencyName;

        public static bool IsDepartmentLoaded(Department department) => LoadedDepartments.Contains(department);

        protected BaseAgency()
        {
            LoadedDepartments.Add(Department);

            EventHandlers[EventNames.LocalDutyChanged] += new Action<bool, int>(OnLocalDutyChanged);
            EventHandlers[EventNames.LocalCalloutReceived] += new Action<string>(OnLocalCalloutReceived);
            EventHandlers["onClientResourceStop"] += new Action<string>(OnResourceStop);
        }

        public virtual Task OnDuty(PlayerData player)
        {
            Log.Debug($"{player.Name} is on duty.");
            return Task.FromResult(0);
        }

        public virtual Task OffDuty(PlayerData player)
        {
            Log.Debug($"{player.Name} is off duty.");
            return Task.FromResult(0);
        }

        public abstract Task OnCalloutReceived(CalloutData callout);

        private async void OnLocalDutyChanged(bool isOnDuty, int departmentId)
        {
            var shouldBeActive = isOnDuty && (Department)departmentId == Department;
            if (shouldBeActive == IsActive) return;

            IsActive = shouldBeActive;
            if (shouldBeActive) Active = this;
            else if (Active == this) Active = null;
            try
            {
                if (shouldBeActive)
                    await OnDuty(CurrentPlayer);
                else
                    await OffDuty(CurrentPlayer);
            }
            catch (Exception ex)
            {
                Log.Error("Duty change failed", ex);
            }
        }

        private async void OnLocalCalloutReceived(string calloutJson)
        {
            if (!IsActive) return;

            try
            {
                var callout = JsonConvert.DeserializeObject<CalloutData>(calloutJson);
                if (callout is null || callout.RequiredDepartment != Department) return;
                await OnCalloutReceived(callout);
            }
            catch (Exception ex)
            {
                Log.Error("Callout handling failed", ex);
            }
        }

        private void OnResourceStop(string resourceName)
        {
            if (resourceName != API.GetCurrentResourceName() || !IsActive) return;

            IsActive = false;
            if (Active == this) Active = null;
            try { _ = OffDuty(CurrentPlayer); }
            catch (Exception ex) { Log.Warn("Cleanup on stop failed", ex); }
        }

        protected static void Notify(string message) => ClientBrain.ShowNotification(message);
    }
}
