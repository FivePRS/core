using System.Collections.Generic;
using System.Text;

namespace FivePRS.Core.Text
{
    public static class GameText
    {
        public const int MaxComponentBytes = 96;

        public static IReadOnlyList<string> SplitComponents(string text, int maxBytes = MaxComponentBytes)
        {
            var parts = new List<string>();
            var start = 0;

            while (start < text.Length)
            {
                var length = NextLength(text, start, maxBytes);
                parts.Add(text.Substring(start, length));
                start += length;
            }

            return parts;
        }

        private static int NextLength(string text, int start, int maxBytes)
        {
            var bytes = 0;
            var inToken = false;
            var safeLength = 0;
            var byteLimitedLength = 0;

            for (var i = start; i < text.Length; i++)
            {
                var charLength = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
                bytes += Encoding.UTF8.GetByteCount(text.Substring(i, charLength));
                if (bytes > maxBytes) break;

                if (text[i] == '~') inToken = !inToken;

                i += charLength - 1;
                byteLimitedLength = i - start + 1;
                if (!inToken) safeLength = byteLimitedLength;
            }

            if (safeLength > 0) return safeLength;
            return byteLimitedLength > 0 ? byteLimitedLength : 1;
        }
    }
}
