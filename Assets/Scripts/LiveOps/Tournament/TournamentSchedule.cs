using System;
using Assets.Scripts.LiveOps;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Back-to-back Golden Tournament windows:
    /// Monday 12:00 UTC → Thursday 12:00 UTC (3.5 days),
    /// Thursday 12:00 UTC → next Monday 12:00 UTC (3.5 days), forever.
    /// </summary>
    public static class TournamentSchedule
    {
        public static readonly TimeSpan Duration = TimeSpan.FromHours(84); // 3.5 days
        public const int ActivationHourUtc = 12;

        public readonly struct Window
        {
            public readonly DateTime StartUtc;
            public readonly DateTime EndUtc;
            public readonly string UniqueId;

            public Window(DateTime startUtc, DateTime endUtc)
            {
                StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
                EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
                UniqueId = $"Tournament_{StartUtc:yyyyMMdd_HH}";
            }

            public bool Contains(DateTime utc) => utc >= StartUtc && utc < EndUtc;
            public TimeSpan Remaining(DateTime utc) => utc >= EndUtc ? TimeSpan.Zero : EndUtc - utc;
        }

        public static Window GetCurrentWindow(DateTime utcNow)
        {
            utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            DateTime start = GetStartOfContainingWindow(utcNow);
            return new Window(start, start + Duration);
        }

        public static bool IsTournamentLiveOp(string eventId)
        {
            return string.Equals(eventId, TournamentLiveOpService.EventId, StringComparison.Ordinal);
        }

        private static DateTime GetStartOfContainingWindow(DateTime utcNow)
        {
            // Candidate starts: most recent Mon 12:00 or Thu 12:00 at or before now.
            DateTime monday = MostRecentWeekdayAtHour(utcNow, DayOfWeek.Monday, ActivationHourUtc);
            DateTime thursday = MostRecentWeekdayAtHour(utcNow, DayOfWeek.Thursday, ActivationHourUtc);
            DateTime start = monday > thursday ? monday : thursday;

            // Safety: if somehow past duration (should not happen with back-to-back), step forward.
            while (start + Duration <= utcNow)
                start = NextStartAfter(start);

            return start;
        }

        private static DateTime NextStartAfter(DateTime start)
        {
            // Mon -> Thu, Thu -> next Mon
            if (start.DayOfWeek == DayOfWeek.Monday)
                return start.AddDays(3);
            return start.AddDays(4);
        }

        private static DateTime MostRecentWeekdayAtHour(DateTime utcNow, DayOfWeek day, int hour)
        {
            int delta = ((int)utcNow.DayOfWeek - (int)day + 7) % 7;
            DateTime candidate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, hour, 0, 0, DateTimeKind.Utc)
                .AddDays(-delta);

            if (candidate > utcNow)
                candidate = candidate.AddDays(-7);

            return candidate;
        }
    }
}
