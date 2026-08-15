using System;
using UnityEngine;

namespace Assets.Scripts.LiveOps.Tournament
{
    public static class TournamentUiFormat
    {
        /// <summary>
        /// Human-readable remaining time. Under 10 minutes uses MM:SS.
        /// </summary>
        public static string FormatTimeLeft(TimeSpan rem)
        {
            if (rem.TotalSeconds <= 0)
                return "0m";

            if (rem.TotalMinutes < 10.0)
            {
                int totalSecs = Mathf.Max(0, (int)rem.TotalSeconds);
                int mm = totalSecs / 60;
                int ss = totalSecs % 60;
                return $"{mm:00}:{ss:00}";
            }

            if (rem.TotalDays >= 1)
                return $"{(int)rem.TotalDays}d {rem.Hours}h";
            if (rem.TotalHours >= 1)
                return $"{(int)rem.TotalHours}h {rem.Minutes}m";
            return $"{Mathf.Max(1, rem.Minutes)}m";
        }
    }
}
