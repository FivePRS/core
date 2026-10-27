using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Events;
using Newtonsoft.Json;

namespace FivePRS.Server.Framework
{
    public sealed class FrameworkCharacter
    {
        public string Id { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DateOfBirth { get; set; }
        public string Gender { get; set; } = "Male";
        public string? Job { get; set; }
        public int Grade { get; set; }
        public string? GradeName { get; set; }
        public bool? OnDuty { get; set; }
        public bool TracksDuty { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
    }

    public sealed class FrameworkOwner
    {
        public string Id { get; set; } = string.Empty;
        public string? License { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Gender { get; set; }
    }

    public sealed class FrameworkVehicle
    {
        public string Plate { get; set; } = string.Empty;
        public string? Model { get; set; }
        public FrameworkOwner? Owner { get; set; }
    }

    public sealed class FrameworkInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
    }

    public sealed class FrameworkBridge
    {
        private const int TimeoutMs = 45_000;

        private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> Pending = new();

        public FrameworkBridge(FrameworkInfo info) => Info = info;

        public FrameworkInfo Info { get; }

        public static string DisplayName(string name) => name switch
        {
            "qbox"   => "Qbox",
            "qbcore" => "QBCore",
            "esx"    => "ESX Legacy",
            "ox"     => "ox_core",
            "nd"     => "ND_Core",
            _        => name,
        };

        public static async Task<FrameworkBridge?> DetectAsync(string preferred)
        {
            var info = await RequestAsync<FrameworkInfo?>("detect", new { preferred });
            return info is null || string.IsNullOrEmpty(info.Name) ? null : new FrameworkBridge(info);
        }

        public Task<FrameworkCharacter?> CharacterAsync(int source, IEnumerable<string> jobs) =>
            RequestAsync<FrameworkCharacter?>("character", new { source, jobs });

        public string ExternalId(string id) => $"{Info.Family}:{id}";

        public async Task<List<FrameworkVehicle>> VehiclesAsync(string characterId)
        {
            var json = await RawAsync("vehicles", new { id = characterId });
            return json.TrimStart().StartsWith("[")
                ? JsonConvert.DeserializeObject<List<FrameworkVehicle>>(json) ?? new List<FrameworkVehicle>()
                : new List<FrameworkVehicle>();
        }

        public Task<FrameworkVehicle?> VehicleAsync(string plate) =>
            RequestAsync<FrameworkVehicle?>("vehicle", new { plate = plate.Replace(" ", string.Empty) });

        public Task<bool> SetDutyAsync(int source, bool onDuty) =>
            RequestAsync<bool>("setDuty", new { source, onDuty });

        public Task<bool> RemoveMoneyAsync(int source, string account, int amount, string reason) =>
            RequestAsync<bool>("removeMoney", new { source, account, amount, reason });

        private static async Task<T> RequestAsync<T>(string method, object args)
        {
            var json = await RawAsync(method, args);
            return string.IsNullOrEmpty(json) ? default! : JsonConvert.DeserializeObject<T>(json)!;
        }

        private static async Task<string> RawAsync(string method, object args)
        {
            await MainThread.Resume();

            var id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<string>();
            Pending[id] = completion;

            try
            {
                BaseScript.TriggerEvent(EventNames.LocalFrameworkRequest, id, method, JsonConvert.SerializeObject(args));

                var finished = await Task.WhenAny(completion.Task, BaseScript.Delay(TimeoutMs));
                await MainThread.Resume();
                if (finished != completion.Task)
                    throw new TimeoutException($"The framework didn't answer '{method}' within {TimeoutMs / 1000} seconds.");

                return await completion.Task;
            }
            finally
            {
                Pending.TryRemove(id, out _);
            }
        }

        public static void OnResponse(string id, string result, string error)
        {
            if (id is null || !Pending.TryGetValue(id, out var completion)) return;

            if (string.IsNullOrEmpty(error))
                completion.TrySetResult(result ?? string.Empty);
            else
                completion.TrySetException(new InvalidOperationException(error));
        }
    }
}
