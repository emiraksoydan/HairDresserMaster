using System.Text.RegularExpressions;

namespace Business.Helpers
{
    public static class SocialMentionParser
    {
        private static readonly Regex MentionRegex = new(
            @"@([a-z0-9_]{3,30})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Yorum metnindeki @kullanici_adi ifadelerini benzersiz ve küçük harfe normalize edilmiş olarak döner.
        /// </summary>
        public static IReadOnlyList<string> ExtractUsernames(string? text, int maxMentions = 10)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in MentionRegex.Matches(text))
            {
                if (!match.Success || match.Groups.Count < 2)
                    continue;

                var username = match.Groups[1].Value.ToLowerInvariant();
                if (username.Length is < 3 or > 30)
                    continue;

                set.Add(username);
                if (set.Count >= maxMentions)
                    break;
            }

            return set.ToList();
        }
    }
}
