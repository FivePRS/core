using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CitizenFX.Core;
using FivePRS.Core.Models;

namespace FivePRS.Client.Callouts
{
    public sealed class RegisteredCallout
    {
        public Type Type { get; }
        public CalloutInfoAttribute Info { get; }

        internal RegisteredCallout(Type type, CalloutInfoAttribute info)
        {
            Type = type;
            Info = info;
        }
    }

    public sealed class CalloutRegistry
    {
        private readonly List<RegisteredCallout> _entries = new();
        private readonly object _lock = new();

        public int Count { get { lock (_lock) return _entries.Count; } }

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
                if (name.StartsWith("System") ||
                    name.StartsWith("Microsoft.") ||
                    name.StartsWith("CitizenFX.") ||
                    name.StartsWith("Mono.") ||
                    name.StartsWith("Newtonsoft.") ||
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

                _entries.Add(new RegisteredCallout(type, attr));
                Debug.WriteLine(
                    $"[CalloutRegistry] {attr.Name,-25} dept={attr.Department,-7} " +
                    $"w={attr.Weight,-4} cd={attr.CooldownSeconds}s");
            }
        }

        public List<CalloutDefinition> GetDefinitions(Department department)
        {
            lock (_lock)
            {
                return _entries
                    .Where(e => e.Info.Department == department)
                    .Select(e => new CalloutDefinition
                    {
                        Name            = e.Info.Name,
                        Department      = e.Info.Department,
                        Priority        = e.Info.Priority,
                        Weight          = e.Info.Weight,
                        CooldownSeconds = e.Info.CooldownSeconds,
                        XPReward        = e.Info.XPReward,
                    })
                    .ToList();
            }
        }

        public RegisteredCallout? FindByName(string name)
        {
            lock (_lock)
            {
                return _entries.FirstOrDefault(
                    e => string.Equals(e.Info.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
