using System;
using System.Collections.Generic;

namespace FivePRS.Client.App
{
    internal static class NuiData
    {
        public static string GetString(IDictionary<string, object> data, string key) =>
            data.TryGetValue(key, out var raw) ? raw?.ToString() ?? string.Empty : string.Empty;

        public static bool TryGetString(IDictionary<string, object> data, string key, out string value)
        {
            value = GetString(data, key);
            return value.Length > 0;
        }

        public static bool GetBool(IDictionary<string, object> data, string key) =>
            data.TryGetValue(key, out var raw) && raw is bool value && value;

        public static bool TryGetInt(IDictionary<string, object> data, string key, out int value)
        {
            value = 0;
            if (!data.TryGetValue(key, out var raw) || raw is null) return false;

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
            {
                return false;
            }
        }
    }
}
