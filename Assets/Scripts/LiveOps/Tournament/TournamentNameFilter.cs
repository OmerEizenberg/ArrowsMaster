using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Lightweight display-name moderation for tournament usernames.
    /// Blocks common insults / hate / sexual / violent terms (English + common obfuscations).
    /// </summary>
    public static class TournamentNameFilter
    {
        public const int MinLength = 2;
        public const int MaxLength = 16;

        private static readonly string[] BlockedExact =
        {
            "admin", "moderator", "mod", "gm", "everyone", "system", "null", "undefined"
        };

        // Kept concise on purpose — covers common hurtful categories without an enormous list.
        private static readonly string[] BlockedRoots =
        {
            "fuck", "fuk", "fck", "shit", "sh1t", "bitch", "btch", "asshole", "a hole",
            "bastard", "dick", "d1ck", "cock", "c0ck", "pussy", "cunt", "cnt",
            "nigger", "nigga", "n1gger", "n1gga", "faggot", "fagg", "fag", "retard", "r3tard",
            "slut", "whore", "rape", "rapist", "pedo", "paedo", "molest",
            "kill yourself", "kys", "suicide", "hang yourself",
            "nazi", "hitler", "kkk",
            "terrorist", "isis",
            "chink", "spic", "kike", "tranny", "dyke"
        };

        private static Regex collapsedWordRegex;

        public static bool TryValidate(string raw, out string cleaned, out string error)
        {
            cleaned = string.Empty;
            error = string.Empty;

            if (raw == null)
            {
                error = "Name is empty.";
                return false;
            }

            cleaned = raw.Trim();
            if (cleaned.Length < MinLength)
            {
                error = $"Name must be at least {MinLength} characters.";
                return false;
            }

            if (cleaned.Length > MaxLength)
                cleaned = cleaned.Substring(0, MaxLength);

            // Allow letters, digits, spaces, underscore, hyphen, period.
            if (!Regex.IsMatch(cleaned, @"^[\p{L}\p{N} _.\-]+$"))
            {
                error = "Name has invalid characters.";
                return false;
            }

            string lowered = cleaned.ToLowerInvariant();
            foreach (var exact in BlockedExact)
            {
                if (lowered == exact)
                {
                    error = "That name is not allowed.";
                    return false;
                }
            }

            string collapsed = CollapseForMatch(cleaned);
            foreach (var root in BlockedRoots)
            {
                string rootCollapsed = CollapseForMatch(root);
                if (string.IsNullOrEmpty(rootCollapsed))
                    continue;

                if (collapsed.Contains(rootCollapsed) || lowered.Contains(root))
                {
                    error = "Please choose a friendlier name.";
                    return false;
                }
            }

            return true;
        }

        /// <summary>Returns a safe name, falling back to Player### if invalid.</summary>
        public static string SanitizeOrFallback(string raw)
        {
            if (TryValidate(raw, out string cleaned, out _))
                return cleaned;
            return "Player" + UnityEngine.Random.Range(0, 1001);
        }

        private static string CollapseForMatch(string value)
        {
            if (collapsedWordRegex == null)
                collapsedWordRegex = new Regex(@"[^a-z0-9]+", RegexOptions.Compiled);

            string lower = value.ToLowerInvariant()
                .Replace('0', 'o')
                .Replace('1', 'i')
                .Replace('3', 'e')
                .Replace('4', 'a')
                .Replace('5', 's')
                .Replace('7', 't')
                .Replace('@', 'a')
                .Replace('$', 's');

            return collapsedWordRegex.Replace(lower, string.Empty);
        }
    }
}
