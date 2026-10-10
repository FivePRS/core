using System;
using System.Collections.Generic;
using System.Linq;

namespace FivePRS.Client
{
    public static class Interactions
    {
        private static readonly List<Func<bool>> Handlers = new();

        public static void Register(Func<bool> handler)
        {
            if (!Handlers.Contains(handler)) Handlers.Add(handler);
        }

        public static void Unregister(Func<bool> handler) => Handlers.Remove(handler);

        internal static bool TryHandle() => Handlers.ToList().Any(handler => handler());
    }
}
