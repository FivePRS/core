using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Agency;
using FivePRS.Client.Arrest;
using FivePRS.Client.Dispatch;
using FivePRS.Client.Menu;
using FivePRS.Client.Terminal;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Core.Police;
using FivePRS.Core.Text;
using Newtonsoft.Json;

namespace FivePRS.Police.TrafficStops
{
    public class TrafficStopManager : BaseScript
    {
        private const float TargetRange = 30f;
        private const float TargetCone = 0.85f;
        private const float PedestrianRange = 2.5f;
        private const float PedestrianCone = 0.5f;
        private const float TalkRange = 3f;
        private const float MenuRange = 4.5f;
        private const float AbandonRange = 150f;
        private const float VehicleEscapeRange = 400f;
        private const float FootEscapeRange = 150f;
        private const int PullOverTimeoutMs = 15_000;
        private const int ScanIntervalMs = 250;
        private const string Subject = "~b~Stop";

        private static TrafficStop? _stop;
        private static Vehicle? _vehicleCandidate;
        private static Ped? _pedestrianCandidate;

        public TrafficStopManager()
        {
            API.RegisterCommand(KeyCommands.TrafficStop, new Action<int, List<object>, string>((_, __, ___) => StartVehicleStop()), false);
            API.RegisterKeyMapping(KeyCommands.TrafficStop, "FivePRS: Traffic stop", "keyboard", "LSHIFT");

            EventHandlers[EventNames.ClientStopResult] += new Action<string, string, string>(OnStopResult);
            EventHandlers[EventNames.ClientLookupError] += new Action<string>(OnLookupError);
            EventHandlers["onClientResourceStop"] += new Action<string>(name =>
            {
                if (name == API.GetCurrentResourceName()) _ = EndAsync(true, null);
            });

            Interactions.Register(TryInteract);
            StreetIdentity.CurrentStop = DescribeStop;
            Tick += OnTick;
        }

        private static StreetStop? DescribeStop()
        {
            var stop = _stop;
            if (stop is null) return null;

            var identity = stop.Identity?.Character;
            return new StreetStop
            {
                Kind         = stop.IsVehicleStop ? "vehicle" : "person",
                Plate        = stop.Plate,
                Model        = stop.Model,
                Name         = identity is null ? null : $"{identity.FirstName} {identity.LastName}",
                CharacterId  = identity?.Id,
                OwnerId      = stop.Registration?.Character.Id,
                PlateChecked = stop.Registration is not null || stop.RegistrationError is not null,
                Checking     = stop.AwaitingPlate,
                RunPlate     = stop.IsVehicleStop ? () => RequestPlate(stop) : null,
            };
        }

        private static bool OnDutyAsPolice => BaseAgency.Active?.Department == Department.Police;

        private async Task OnTick()
        {
            await Delay(ScanIntervalMs);

            if (_stop is not null)
            {
                await UpdateStopAsync(_stop);
                return;
            }

            _vehicleCandidate    = OnDutyAsPolice ? FindVehicle() : null;
            _pedestrianCandidate = OnDutyAsPolice && _vehicleCandidate is null && !CompactMenu.IsOpen ? FindPedestrian() : null;

            if (_vehicleCandidate is not null)
                ClientBrain.ShowHelp($"{Binds.TrafficStop} Pull over this vehicle", ScanIntervalMs + 100);
            else if (_pedestrianCandidate is not null)
                ClientBrain.ShowHelp($"{Binds.Interact} Stop and talk to this person", ScanIntervalMs + 100);
        }

        private static Vehicle? FindVehicle()
        {
            var player = Game.PlayerPed;
            var own    = player.CurrentVehicle;
            if (own is null || !own.Exists() || own.Driver != player || !API.IsVehicleSirenOn(own.Handle)) return null;

            Vehicle? best = null;
            var bestDistance = TargetRange;

            foreach (var vehicle in World.GetAllVehicles())
            {
                if (vehicle.Handle == own.Handle || !vehicle.Exists() || API.IsEntityAMissionEntity(vehicle.Handle)) continue;

                var driver = vehicle.Driver;
                if (driver is null || !driver.Exists() || driver.IsPlayer || driver.IsDead) continue;

                var offset   = vehicle.Position - own.Position;
                var distance = offset.Length();
                if (distance >= bestDistance || distance < 1f) continue;

                offset.Normalize();
                if (Vector3.Dot(offset, own.ForwardVector) < TargetCone) continue;

                best = vehicle;
                bestDistance = distance;
            }

            return best;
        }

        private static Ped? FindPedestrian()
        {
            var player = Game.PlayerPed;
            if (player.IsInVehicle() || player.IsDead || ArrestManager.HasCustody) return null;

            Ped? best = null;
            var bestDistance = PedestrianRange;

            foreach (var ped in World.GetAllPeds())
            {
                if (ped.Handle == player.Handle || !ped.Exists() || ped.IsPlayer || ped.IsDead || ped.IsInVehicle()) continue;
                if (!API.IsPedHuman(ped.Handle) || API.IsEntityAMissionEntity(ped.Handle)) continue;

                var offset   = ped.Position - player.Position;
                var distance = offset.Length();
                if (distance >= bestDistance) continue;

                offset.Normalize();
                if (Vector3.Dot(offset, player.ForwardVector) < PedestrianCone) continue;

                best = ped;
                bestDistance = distance;
            }

            return best;
        }

        private static void StartVehicleStop()
        {
            if (_stop is not null || !OnDutyAsPolice) return;

            var vehicle = _vehicleCandidate ?? FindVehicle();
            var driver  = vehicle?.Driver;
            if (vehicle is null || driver is null || !driver.Exists()) return;

            _vehicleCandidate = null;
            _stop = new TrafficStop(driver, vehicle, StreetIdentity.SeedFor(driver));
            _stop.PullOver();

            ClientBrain.ShowNotification($"Pulling over the ~y~{_stop.Model}~w~, plate ~y~{_stop.Plate}~w~.", Subject);
            TerminalApps.NotifyChanged();
        }

        private static bool StartPedestrianStop(Ped ped)
        {
            if (_stop is not null || !OnDutyAsPolice || !ped.Exists() || ped.IsDead) return false;

            _pedestrianCandidate = null;
            var stop = new TrafficStop(ped, null, StreetIdentity.SeedFor(ped));
            _stop = stop;
            TerminalApps.NotifyChanged();

            if (stop.Person.WillFlee)
            {
                stop.Flee();
                ClientBrain.ShowNotification("~r~They're running!~w~ Catch them and press " + Binds.Cuff + " to cuff them.", Subject);
                return true;
            }

            stop.Hold();
            OpenMenu(stop);
            return true;
        }

        private async Task UpdateStopAsync(TrafficStop stop)
        {
            if (!OnDutyAsPolice || !stop.IsValid)
            {
                await EndAsync(true, stop.IsValid ? null : "The stop has ended.");
                return;
            }

            var distance = Vector3.Distance(Game.PlayerPed.Position, stop.Anchor.Position);

            switch (stop.Phase)
            {
                case StopPhase.PullingOver:
                    var elapsed = API.GetGameTimer() - stop.StartedAt;
                    if (stop.Person.WillFlee && stop.Vehicle!.Speed < 4f)
                    {
                        stop.Flee();
                        ClientBrain.ShowNotification($"~r~The driver is fleeing!~w~ Pursue the ~y~{stop.Model}~w~.", Subject);
                    }
                    else if ((stop.Vehicle!.Speed < 0.5f && elapsed > 2_000) || elapsed > PullOverTimeoutMs || stop.StrayedWhilePullingOver)
                    {
                        stop.Hold();
                        ClientBrain.ShowNotification($"Vehicle stopped. Approach the driver and press {Binds.Interact}.", Subject);
                    }
                    break;

                case StopPhase.Fleeing:
                    if (stop.InCustody)
                    {
                        stop.Hold();
                        ClientBrain.ShowNotification($"Suspect in custody. Press {Binds.Interact} near them to continue the stop.", Subject);
                        return;
                    }

                    if (distance > (stop.IsVehicleStop && stop.DriverInVehicle ? VehicleEscapeRange : FootEscapeRange))
                        await EndAsync(false, "~r~The suspect got away.");
                    break;

                case StopPhase.Stopped:
                    if (distance > AbandonRange)
                    {
                        await EndAsync(true, $"You left the stop, so {Noun(stop)} was released.");
                        return;
                    }

                    if (!CompactMenu.IsOpen && !Game.PlayerPed.IsInVehicle() && DriverDistance(stop) <= TalkRange)
                        ClientBrain.ShowHelp($"{Binds.Interact} Talk to {Noun(stop)}", ScanIntervalMs + 100);
                    break;
            }
        }

        private static string Noun(TrafficStop stop) => stop.IsVehicleStop ? "the driver" : "the person";

        private static float DriverDistance(TrafficStop stop) =>
            Vector3.Distance(Game.PlayerPed.Position, stop.Driver.Position);

        private static bool TryInteract()
        {
            var stop = _stop;
            if (stop is null)
            {
                var ped = _pedestrianCandidate;
                return ped is not null && DistanceTo(ped) <= TalkRange && StartPedestrianStop(ped);
            }

            if (stop.Phase != StopPhase.Stopped || !stop.IsValid) return false;
            if (Game.PlayerPed.IsInVehicle() || DriverDistance(stop) > TalkRange) return false;

            OpenMenu(stop);
            return true;
        }

        private static float DistanceTo(Ped ped) => ped.Exists() ? Vector3.Distance(Game.PlayerPed.Position, ped.Position) : float.MaxValue;

        private static void OpenMenu(TrafficStop stop) =>
            CompactMenu.Open(() => StopPage(stop), () =>
                _stop == stop && stop.IsValid && !Game.PlayerPed.IsInVehicle() && DriverDistance(stop) <= MenuRange,
                ConfigManager.Settings.Branding.Departments.Police);

        private static MenuPage StopPage(TrafficStop stop)
        {
            var identified = stop.CharacterId is not null;
            var name = identified ? $"{stop.Identity!.Character.FirstName} {stop.Identity.Character.LastName}" : null;
            var page = new MenuPage
            {
                Title    = stop.IsVehicleStop ? "Traffic stop" : "Pedestrian stop",
                Subtitle = stop.IsVehicleStop ? $"{stop.Model} · {stop.Plate}" : name ?? "Unidentified",
            };

            page.Items.Add(new MenuItem
            {
                Label    = "Ask for ID",
                Detail   = name ?? (stop.AwaitingIdentity ? "Checking…" : null),
                Disabled = stop.AwaitingIdentity,
                Select   = () => Run(() => RequestIdentity(stop)),
            });

            if (stop.IsVehicleStop)
            {
                page.Items.Add(new MenuItem
                {
                    Label    = "Run plate",
                    Detail   = stop.Registration is not null ? "Done" : stop.RegistrationError is not null ? "No record" : stop.AwaitingPlate ? "Checking…" : stop.Plate,
                    Disabled = stop.AwaitingPlate,
                    Select   = () => Run(() => RequestPlate(stop)),
                });

                if (!stop.InCustody)
                {
                    page.Items.Add(stop.DriverInVehicle
                        ? new MenuItem { Label = "Ask to step out", Select = () => Run(stop.StepOut) }
                        : new MenuItem { Label = "Ask to get back in", Select = () => Run(stop.GetBackIn) });
                }
            }

            page.Items.Add(new MenuItem
            {
                Label    = stop.IsVehicleStop ? "Search the driver" : "Search them",
                Detail   = stop.DriverInVehicle ? "Step out first" : stop.Searched ? "Done" : null,
                Disabled = stop.DriverInVehicle,
                Select   = () => Run(() => Search(stop)),
            });

            page.Items.Add(new MenuItem
            {
                Label    = "Issue a warning",
                Detail   = identified ? null : "ID first",
                Disabled = !identified,
                Submenu  = () => OffencePage(stop, RecordType.Warning),
            });

            page.Items.Add(new MenuItem
            {
                Label    = "Issue a citation",
                Detail   = identified ? null : "ID first",
                Disabled = !identified,
                Submenu  = () => OffencePage(stop, RecordType.Citation),
            });

            foreach (var warrant in stop.ActiveWarrants)
            {
                page.Items.Add(new MenuItem
                {
                    Label  = "Serve warrant",
                    Detail = warrant.Description,
                    Select = () => Run(() => ServeWarrant(warrant)),
                });
            }

            page.Items.Add(stop.InCustody
                ? new MenuItem
                {
                    Label    = "Record the arrest",
                    Detail   = identified ? null : "ID first",
                    Disabled = !identified,
                    Select   = () => Run(() => RecordArrest(stop)),
                }
                : new MenuItem { Label = "Arrest", Detail = $"Press {Binds.Cuff}", Disabled = true });

            page.Items.Add(BackupClient.MenuEntry());

            page.Items.Add(new MenuItem
            {
                Label  = stop.InCustody ? "End the stop" : stop.IsVehicleStop ? "Release the driver" : "Let them go",
                Select = async () =>
                {
                    CompactMenu.Close();
                    await EndAsync(!stop.InCustody, stop.InCustody ? "Stop ended." : "Released.");
                },
            });

            return page;
        }

        private static MenuPage OffencePage(TrafficStop stop, RecordType type)
        {
            var page = new MenuPage
            {
                Title    = type == RecordType.Citation ? "Citation" : "Warning",
                Subtitle = stop.Identity is null ? null : $"{stop.Identity.Character.FirstName} {stop.Identity.Character.LastName}",
            };

            foreach (var offence in ConfigManager.Citations.Offences)
            {
                var fine = type == RecordType.Citation ? Math.Max(0, Math.Min(offence.Fine, ConfigManager.Settings.MaxFine)) : 0;
                page.Items.Add(new MenuItem
                {
                    Label  = offence.Label,
                    Detail = type == RecordType.Citation ? $"${fine}" : null,
                    Select = () => Run(() => Issue(stop, type, offence.Label, fine)),
                });
            }

            return page;
        }

        private static Task Run(Action action)
        {
            action();
            return Task.FromResult(0);
        }

        private static void RequestIdentity(TrafficStop stop)
        {
            if (stop.AwaitingIdentity) return;
            stop.AwaitingIdentity = true;
            ClientEvents.TriggerServer(EventNames.ServerStopIdentify, stop.Seed, stop.Female);
        }

        private static void RequestPlate(TrafficStop stop)
        {
            if (stop.AwaitingPlate) return;
            stop.AwaitingPlate = true;
            ClientEvents.TriggerServer(EventNames.ServerStopPlate, stop.Plate, stop.Model, stop.Seed, stop.Female);
        }

        private static void Search(TrafficStop stop)
        {
            stop.Searched = true;
            ClientBrain.ShowNotification(stop.Person.Contraband is null
                ? "You found nothing of interest."
                : $"~o~Found:~w~ {stop.Person.Contraband}.", "~b~Search");
        }

        private static void Issue(TrafficStop stop, RecordType type, string offence, int fine)
        {
            if (stop.CharacterId is null) return;

            ClientEvents.TriggerServer(EventNames.ServerRecordIssue, stop.CharacterId.Value, (int)type, offence, fine);
            ClientBrain.ShowNotification(type == RecordType.Citation
                ? $"Citation issued: {offence} (~g~${fine}~w~)."
                : $"Warning issued: {offence}.", Subject);
            OpenMenu(stop);
        }

        private static void ServeWarrant(RecordInfo warrant)
        {
            ClientEvents.TriggerServer(EventNames.ServerRecordResolve, warrant.Id, true);
            warrant.Active = false;
            ClientBrain.ShowNotification($"Warrant served: {warrant.Description}.", Subject);
        }

        private static void RecordArrest(TrafficStop stop)
        {
            if (stop.CharacterId is null) return;

            var warrants = stop.ActiveWarrants;
            var where    = stop.IsVehicleStop ? "a traffic stop" : "a pedestrian stop";
            var reason   = warrants.Length > 0
                ? $"Arrested during {where} on a warrant: {warrants[0].Description}"
                : $"Arrested during {where}";
            ClientEvents.TriggerServer(EventNames.ServerRecordIssue, stop.CharacterId.Value, (int)RecordType.Arrest, reason, 0);
            ClientBrain.ShowNotification("Arrest recorded.", Subject);
        }

        private void OnStopResult(string kind, string json, string error)
        {
            var stop = _stop;
            if (stop is null) return;

            var record = string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<LookupResult>(json)?.Record;

            if (kind == "person")
            {
                stop.AwaitingIdentity = false;
                if (record is null)
                {
                    ClientBrain.ShowNotification($"~r~{error}", "~b~ID check");
                }
                else
                {
                    stop.Identity = record;
                    ClientBrain.ShowNotification(DescribePerson(record), "~b~ID check");
                }
            }
            else
            {
                stop.AwaitingPlate = false;
                if (record is null)
                {
                    stop.RegistrationError = error;
                    ClientBrain.ShowNotification($"~o~{error}", "~b~Plate check");
                }
                else
                {
                    stop.Registration = record;
                    ClientBrain.ShowNotification(DescribeVehicle(record, stop.Plate), "~b~Plate check");
                }
            }

            CompactMenu.Refresh();
            TerminalApps.NotifyChanged();
        }

        private void OnLookupError(string error)
        {
            if (_stop is not null && CompactMenu.IsOpen)
                ClientBrain.ShowNotification($"~r~{error}", Subject);
        }

        private static string DescribePerson(LookupRecord record)
        {
            var person  = record.Character;
            var license = record.Licenses.FirstOrDefault(l => l.Type == StreetProfiles.DriverLicenseType)?.Status;
            var licenseText = license switch
            {
                LicenseStatus.Valid     => "~g~Valid license",
                LicenseStatus.Suspended => "~o~License suspended",
                LicenseStatus.Revoked   => "~r~License revoked",
                _                       => "~r~No license",
            };
            var warrant = person.HasActiveWarrant ? "~r~ACTIVE WARRANT" : "~g~No warrants";
            return $"{person.FirstName} {person.LastName} · DOB {person.DateOfBirth}~n~{licenseText}~w~ · {warrant}";
        }

        private static string DescribeVehicle(LookupRecord record, string plate)
        {
            var vehicle = record.Vehicles.FirstOrDefault(v => string.Equals(v.Plate, record.MatchedPlate ?? plate, StringComparison.OrdinalIgnoreCase));
            var status  = vehicle?.Status == VehicleStatus.Stolen ? "~r~REPORTED STOLEN" : "~g~Valid registration";
            var owner   = record.Character.HasActiveWarrant ? " ~r~(owner has a warrant)" : string.Empty;
            return $"{plate} · {vehicle?.Model ?? "Unknown"}~n~Registered to {record.Character.FirstName} {record.Character.LastName}{owner}~n~{status}";
        }

        private static async Task EndAsync(bool release, string? message)
        {
            var stop = _stop;
            if (stop is null) return;

            _stop = null;
            TerminalApps.NotifyChanged();
            if (CompactMenu.IsOpen) CompactMenu.Close();
            if (message is not null) ClientBrain.ShowNotification(message, Subject);

            await stop.EndAsync(release);
        }
    }
}
