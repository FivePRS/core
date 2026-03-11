using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CitizenFX.Core;
using FivePRS.Core.Models;

namespace FivePRS.Client.Callouts
{
    /// <summary>
    /// Maintains a catalog of all registered <see cref="CalloutBase"/> subclasses and
    /// handles weighted, cooldown-aware random selection for the dispatcher.
    ///
    /// Registration is driven entirely by the <see cref="CalloutInfoAttribute"/> — there
    /// is no central list to maintain. To add a callout, decorate a class and call
    /// <see cref="Discover(Assembly)"/> with its assembly. That's it.
    /// </summary>
    public sealed class CalloutRegistry
    {
        private sealed class CalloutEntry
        {
            public Type                  Type          { get; }
            public CalloutInfoAttribute  Info          { get; }
            public long                  LastDispatchedMs { get; set; }

            public CalloutEntry(Type type, CalloutInfoAttribute info)
            {
                Type             = type;
                Info             = info;
                LastDispatchedMs = 0;
            }

            public bool IsOnCooldown =>
                Info.CooldownSeconds > 0 &&
                (Environment.TickCount64 - LastDispatchedMs) < (Info.CooldownSeconds * 1000L);
        }

        private readonly List<CalloutEntry> _entries = new();
        private readonly object _lock = new();

        public void Discover(Assembly assembly)
        {
            if (assembly is null) throw new ArgumentNullException(nameof(assembly));

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || !type.IsSubclassOf(typeof(CalloutBase))) continue;

                var attr = type.GetCustomAttribute<CalloutInfoAttribute>();
                if (attr is null) continue;

                Register(type, attr);
            }
        }

        public void DiscoverAll()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                var name = asm.GetName().Name ?? "";
                if (name.StartsWith("System.") ||
                    name.StartsWith("Microsoft.") ||
                    name.StartsWith("CitizenFX.") ||
                    name == "netstandard" ||
                    name == "mscorlib") continue;

                try { Discover(asm); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CalloutRegistry] Skipped {name}: {ex.Message}");
                }
            }
        }

        public void Register<T>() where T : CalloutBase
        {
            var attr = typeof(T).GetCustomAttribute<CalloutInfoAttribute>()
                ?? throw new InvalidOperationException(
                    $"{typeof(T).Name} is missing [CalloutInfo(...)]. Cannot register.");
            Register(typeof(T), attr);
        }

        private void Register(Type type, CalloutInfoAttribute attr)
        {
            lock (_lock)
            {
                if (_entries.Any(e => e.Type == type)) return;

                _entries.Add(new CalloutEntry(type, attr));
                Debug.WriteLine(
                    $"[CalloutRegistry] ✓ {attr.Name,-25} dept={attr.Department,-7} " +
                    $"w={attr.Weight,-4} cd={attr.CooldownSeconds}s");
            }
        }

        public (Type? Type, CalloutInfoAttribute? Info) PickCallout(
            Department department, int maxAttempts = 5)
        {
            lock (_lock)
            {
                var now = Environment.TickCount64;

                var pool = _entries
                    .Where(e => e.Info.Department == department && !e.IsOnCooldown)
                    .ToList();

                if (pool.Count == 0) return (null, null);

                var rng         = new Random();
                var usedIndices = new HashSet<int>();

                for (var attempt = 0; attempt < maxAttempts && usedIndices.Count < pool.Count; attempt++)
                {
                    var winner = WeightedRandom(pool, usedIndices, rng);
                    if (winner is null) break;

                    CalloutBase? probe = null;
                    try { probe = (CalloutBase)Activator.CreateInstance(winner.Type)!; }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CalloutRegistry] Probe instantiation failed for {winner.Type.Name}: {ex.Message}");
                        usedIndices.Add(pool.IndexOf(winner));
                        continue;
                    }

                    if (!probe.CanBeDispatched())
                    {
                        usedIndices.Add(pool.IndexOf(winner));
                        continue;
                    }

                    winner.LastDispatchedMs = now;
                    return (winner.Type, winner.Info);
                }

                return (null, null);
            }
        }

        public (Type? Type, CalloutInfoAttribute? Info) FindByName(string name)
        {
            lock (_lock)
            {
                var entry = _entries.FirstOrDefault(
                    e => string.Equals(e.Info.Name, name, StringComparison.OrdinalIgnoreCase));

                return entry is null ? (null, null) : (entry.Type, entry.Info);
            }
        }

        public int Count { get { lock (_lock) return _entries.Count; } }

        private static CalloutEntry? WeightedRandom(
            List<CalloutEntry> pool, HashSet<int> excludeIndices, Random rng)
        {
            var eligible = pool
                .Select((entry, idx) => (entry, idx))
                .Where(x => !excludeIndices.Contains(x.idx))
                .ToList();

            if (eligible.Count == 0) return null;

            var totalWeight = eligible.Sum(x => x.entry.Info.Weight);
            var roll        = rng.Next(totalWeight);
            var running     = 0;

            foreach (var (entry, idx) in eligible)
            {
                running += entry.Info.Weight;
                if (roll < running) return entry;
            }

            return eligible[^1].entry;
        }
    }
}
