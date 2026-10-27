using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Agency;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Core.Police;
using FivePRS.Core.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Police.Radar
{
    public class RadarManager : BaseScript
    {
        private const int UpdateMs = 200;
        private const int PlateScanMs = 1_000;
        private const int PlateMemoryMs = 120_000;
        private const int EmergencyClass = 18;
        private const float AntennaCone = 0.97f;
        private const float PlateCone = 0.95f;
        private const float MovingSpeed = 1f;
        private const float TurnErrorDegrees = 7f;
        private const string Subject = "~b~ALPR";
        private const string OperatorCallback = "radarOperator";
        private const string RemoteClosedCallback = "radarRemoteClosed";

        private static readonly JsonSerializerSettings JsonSettings = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        private static readonly Dictionary<string, string> FlagNames = new()
        {
            [PlateFlag.Stolen]       = "Stolen vehicle",
            [PlateFlag.Warrant]      = "Owner has a warrant",
            [PlateFlag.Unregistered] = "Unregistered",
            [PlateFlag.Suspended]    = "Owner's license suspended",
        };

        private sealed class Reading
        {
            public int? Target { get; set; }
            public int? Fast { get; set; }
            public string? Direction { get; set; }
            public bool Error { get; set; }
        }

        private sealed class Antenna
        {
            public Antenna(string name, float sign)
            {
                Name = name;
                Sign = sign;
            }

            public string Name { get; }
            public float Sign { get; }
            public Reading Reading { get; set; } = new();
            public bool Locked { get; set; }
        }

        private sealed class PlateRead
        {
            public string Plate { get; set; } = string.Empty;
            public string? Model { get; set; }
            public List<string> Flags { get; set; } = new();
            public bool Checked { get; set; }
        }

        private readonly Dictionary<string, int> _recentPlates = new();
        private readonly Dictionary<string, string> _plateDirection = new();
        private readonly Dictionary<string, PlateRead> _reads = new();
        private readonly Antenna _front = new("front", 1f);
        private readonly Antenna _rear = new("rear", -1f);

        private RadarOperator? _operator;
        private bool _on;
        private bool _shown;
        private bool _remoteOpen;
        private float? _lastHeading;
        private int _nextPlateScan;
        private string _lastPayload = string.Empty;

        public RadarManager()
        {
            API.RegisterCommand(KeyCommands.Radar, new Action<int, List<object>, string>((_, __, ___) => Toggle()), false);
            API.RegisterKeyMapping(KeyCommands.Radar, "FivePRS: Toggle radar", "keyboard", "F7");
            API.RegisterCommand(KeyCommands.RadarLock, new Action<int, List<object>, string>((_, __, ___) => ToggleLock(_front)), false);
            API.RegisterKeyMapping(KeyCommands.RadarLock, "FivePRS: Lock front radar antenna", "keyboard", "J");
            API.RegisterCommand(KeyCommands.RadarRearLock, new Action<int, List<object>, string>((_, __, ___) => ToggleLock(_rear)), false);
            API.RegisterKeyMapping(KeyCommands.RadarRearLock, "FivePRS: Lock rear radar antenna", "keyboard", "U");
            API.RegisterCommand(KeyCommands.RadarRemote, new Action<int, List<object>, string>((_, __, ___) => OpenRemote()), false);
            API.RegisterKeyMapping(KeyCommands.RadarRemote, "FivePRS: Radar operator menu", "keyboard", "F9");

            API.RegisterNuiCallbackType(OperatorCallback);
            EventHandlers[$"__cfx_nui:{OperatorCallback}"] += new Action<IDictionary<string, object>, CallbackDelegate>(OnOperatorChanged);
            API.RegisterNuiCallbackType(RemoteClosedCallback);
            EventHandlers[$"__cfx_nui:{RemoteClosedCallback}"] += new Action<IDictionary<string, object>, CallbackDelegate>((_, callback) =>
            {
                CloseRemote();
                callback("ok");
            });

            EventHandlers[EventNames.ClientPlateRead] += new Action<string>(OnPlateRead);
            EventHandlers["onClientResourceStop"] += new Action<string>(name =>
            {
                if (name != API.GetCurrentResourceName()) return;
                CloseRemote();
                Hide();
            });

            Tick += OnTick;
        }

        private static RadarSettings Settings => ConfigManager.Settings.Radar;

        private RadarOperator Operator => _operator ??= RadarOperator.Load(Settings.SpeedAlert);

        private static bool OnDutyAsPolice => BaseAgency.Active?.Department == Department.Police;

        private void Toggle()
        {
            if (!Settings.Enabled) return;
            if (!OnDutyAsPolice || PatrolCar() is null)
            {
                ClientBrain.ShowNotification("The radar works from a patrol car while you're on duty with Police.", "~b~Radar");
                return;
            }

            _on = !_on;
            _front.Locked = false;
            _rear.Locked = false;
            if (!_on)
            {
                CloseRemote();
                Hide();
            }
        }

        private void ToggleLock(Antenna antenna)
        {
            if (!_on || !_shown) return;
            antenna.Locked = !antenna.Locked;
            Sound(antenna.Locked ? "lock" : "unlock");
            Send(true);
        }

        private void OpenRemote()
        {
            if (!Settings.Enabled) return;
            if (!_on)
            {
                ClientBrain.ShowNotification("Turn the radar on first.", "~b~Radar");
                return;
            }

            _remoteOpen = true;
            API.SetNuiFocus(true, true);
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "radar", type = "remote", payload = Operator }, JsonSettings));
        }

        private void CloseRemote()
        {
            if (!_remoteOpen) return;
            _remoteOpen = false;
            API.SetNuiFocus(false, false);
            API.SendNuiMessage("{\"screen\":\"radar\",\"type\":\"closeRemote\"}");
        }

        private void OnOperatorChanged(IDictionary<string, object> data, CallbackDelegate callback)
        {
            var updated = JObject.FromObject(data).ToObject<RadarOperator>();
            if (updated is not null)
            {
                _operator = updated.Clamped();
                _operator.Save();
                Send(true);
            }
            callback("ok");
        }

        private static Vehicle? PatrolCar()
        {
            var vehicle = Game.PlayerPed.CurrentVehicle;
            return vehicle is not null && vehicle.Exists() && API.GetVehicleClass(vehicle.Handle) == EmergencyClass ? vehicle : null;
        }

        private async Task OnTick()
        {
            await Delay(UpdateMs);

            var vehicle = PatrolCar();
            if (!_on || !Settings.Enabled || !OnDutyAsPolice || vehicle is null)
            {
                if (_on && (!OnDutyAsPolice || vehicle is null))
                {
                    _on = false;
                    CloseRemote();
                }
                Hide();
                return;
            }

            var error = Unstable(vehicle);
            Update(_front, Operator.Front, vehicle, error);
            Update(_rear, Operator.Rear, vehicle, error);

            var now = API.GetGameTimer();
            if (Settings.PlateReader && now >= _nextPlateScan)
            {
                _nextPlateScan = now + PlateScanMs;
                ScanPlate(vehicle, "front", now);
                ScanPlate(vehicle, "rear", now);
                Forget(now);
            }

            Send(false);
        }

        private bool Unstable(Vehicle own)
        {
            var heading = own.Heading;
            var turning = _lastHeading.HasValue && Math.Abs(((heading - _lastHeading.Value + 540f) % 360f) - 180f) > TurnErrorDegrees;
            _lastHeading = heading;
            return turning || !API.IsVehicleOnAllWheels(own.Handle);
        }

        private void Update(Antenna antenna, RadarAntennaSetup setup, Vehicle own, bool error)
        {
            if (antenna.Locked) return;
            if (!setup.Xmit)
            {
                antenna.Reading = new Reading();
                return;
            }

            antenna.Reading = error ? new Reading { Error = true } : Measure(own, antenna.Sign, setup.Mode);

            var limit = Operator.FastLimit;
            var fastest = Math.Max(antenna.Reading.Target ?? 0, antenna.Reading.Fast ?? 0);
            if (Operator.FastLock && limit > 0 && fastest >= limit)
            {
                antenna.Locked = true;
                Sound("fast");
            }
        }

        private static Reading Measure(Vehicle own, float sign, string mode)
        {
            var reading = new Reading();
            var closest = Settings.Range;
            var fastest = -1f;
            var targetSpeed = -1f;
            var ownVelocity = own.Velocity;
            var forward = own.ForwardVector;

            foreach (var vehicle in World.GetAllVehicles())
            {
                if (vehicle.Handle == own.Handle || !vehicle.Exists()) continue;

                var offset = vehicle.Position - own.Position;
                var distance = offset.Length();
                if (distance > Settings.Range || distance < 2f) continue;

                offset.Normalize();
                if (Vector3.Dot(offset, forward) * sign < AntennaCone) continue;

                var velocity = vehicle.Velocity;
                var speed = velocity.Length();
                if (speed < MovingSpeed) continue;

                var same = Vector3.Dot(velocity, forward) > 0f;
                if (same != (mode == RadarOperator.Same)) continue;

                if (distance < closest)
                {
                    closest = distance;
                    targetSpeed = speed;
                    reading.Target = Settings.ToDisplaySpeed(speed);
                    reading.Direction = Vector3.Dot(velocity - ownVelocity, offset) < 0f ? "approach" : "recede";
                }
                if (speed > fastest) fastest = speed;
            }

            if (fastest > targetSpeed && reading.Target.HasValue && Settings.ToDisplaySpeed(fastest) > reading.Target)
                reading.Fast = Settings.ToDisplaySpeed(fastest);

            return reading;
        }

        private void ScanPlate(Vehicle own, string direction, int now)
        {
            var sign = direction == "front" ? 1f : -1f;
            Vehicle? nearest = null;
            var closest = Settings.PlateReaderRange;

            foreach (var vehicle in World.GetAllVehicles())
            {
                if (vehicle.Handle == own.Handle || !vehicle.Exists()) continue;

                var offset = vehicle.Position - own.Position;
                var distance = offset.Length();
                if (distance > closest || distance < 2f) continue;

                offset.Normalize();
                if (Vector3.Dot(offset, own.ForwardVector) * sign < PlateCone) continue;

                nearest = vehicle;
                closest = distance;
            }

            if (nearest is null) return;

            var plate = StreetIdentity.PlateOf(nearest);
            if (plate.Length == 0) return;

            _plateDirection[direction] = plate;
            if (_recentPlates.TryGetValue(plate, out var seen) && now - seen < PlateMemoryMs) return;

            _recentPlates[plate] = now;
            _reads[plate] = new PlateRead { Plate = plate, Model = StreetIdentity.VehicleName(nearest) };
            ClientEvents.TriggerServer(EventNames.ServerPlateRead, plate);
        }

        private void Forget(int now)
        {
            foreach (var plate in _recentPlates.Where(entry => now - entry.Value >= PlateMemoryMs).Select(entry => entry.Key).ToList())
            {
                _recentPlates.Remove(plate);
                if (!_plateDirection.ContainsValue(plate)) _reads.Remove(plate);
            }
        }

        private void OnPlateRead(string json)
        {
            var check = JsonConvert.DeserializeObject<PlateCheck>(json);
            if (check is null) return;

            var key = _reads.Keys.FirstOrDefault(plate => string.Equals(plate.Replace(" ", string.Empty), check.Plate.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase));
            if (key is null) return;

            var read = _reads[key];
            read.Flags = check.Flags;
            read.Checked = true;
            if (!string.IsNullOrEmpty(check.Model) && check.Model != "Unknown") read.Model = check.Model;

            if (read.Flags.Count > 0)
            {
                var reasons = string.Join(" · ", read.Flags.Select(flag => FlagNames.TryGetValue(flag, out var name) ? name : flag));
                ClientBrain.ShowNotification($"~r~HIT~w~ {read.Plate} · {read.Model}~n~{reasons}", Subject);
                Sound("hit");
            }
            Send(true);
        }

        private object? PlatePayload(string direction)
        {
            if (!_plateDirection.TryGetValue(direction, out var plate) || !_reads.TryGetValue(plate, out var read)) return null;
            return new { read.Plate, read.Model, read.Flags, read.Checked };
        }

        private object AntennaPayload(Antenna antenna, RadarAntennaSetup setup)
        {
            var limit = Operator.FastLimit;
            var reading = antenna.Reading;
            return new
            {
                setup.Xmit,
                setup.Mode,
                reading.Target,
                reading.Fast,
                reading.Direction,
                reading.Error,
                antenna.Locked,
                Alert = limit > 0 && Math.Max(reading.Target ?? 0, reading.Fast ?? 0) >= limit,
            };
        }

        private void Send(bool force)
        {
            var vehicle = PatrolCar();
            var op = Operator;
            var payload = JsonConvert.SerializeObject(new
            {
                Units       = Settings.Metric ? "KM/H" : "MPH",
                Patrol      = vehicle is null ? 0 : Settings.ToDisplaySpeed(vehicle.Speed),
                Front       = AntennaPayload(_front, op.Front),
                Rear        = AntennaPayload(_rear, op.Rear),
                op.FastLimit,
                op.FastLock,
                PlateReader = Settings.PlateReader,
                Anchor      = HudAnchor.Minimap(),
                Layout      = new { op.X, op.Y, op.Scale },
                Audio       = new { op.Doppler, op.Beep },
                Plates      = new { Front = PlatePayload("front"), Rear = PlatePayload("rear") },
            }, JsonSettings);

            if (!force && _shown && payload == _lastPayload) return;
            _lastPayload = payload;
            _shown = true;
            API.SendNuiMessage("{\"screen\":\"radar\",\"type\":\"show\",\"payload\":" + payload + "}");
        }

        private void Sound(string kind) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "radar", type = "sound", payload = new { kind, volume = Operator.Beep } }));

        private void Hide()
        {
            _lastHeading = null;
            if (!_shown) return;
            _shown = false;
            _lastPayload = string.Empty;
            _plateDirection.Clear();
            API.SendNuiMessage("{\"screen\":\"radar\",\"type\":\"hide\"}");
        }
    }
}
