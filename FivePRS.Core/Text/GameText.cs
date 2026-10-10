using System.Collections.Generic;

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

            var i = start;
            while (i < text.Length)
            {
                var c = text[i];
                var isPair = c >= '\uD800' && c <= '\uDBFF' && i + 1 < text.Length;
                var charLength = isPair ? 2 : 1;

                bytes += isPair ? 4 : Utf8Length(c);
                if (bytes > maxBytes) break;

                if (c == '~') inToken = !inToken;

                i += charLength;
                byteLimitedLength = i - start;
                if (!inToken) safeLength = byteLimitedLength;
            }

            if (safeLength > 0) return safeLength;
            return byteLimitedLength > 0 ? byteLimitedLength : 1;
        }

        private static int Utf8Length(char c)
        {
            if (c < 0x80) return 1;
            if (c < 0x800) return 2;
            return 3;
        }
    }
}
