using System;
using System.Globalization;

namespace Assets.Scripts.GAE
{
    public static class GAESchedule
    {
        public const double EventDurationHours = 48d;
        private static readonly DateTime EventAnchorUtc = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc);

        public static DateTime GetCurrentEventStartUtc(DateTime utcNow)
        {
            TimeSpan duration = TimeSpan.FromHours(EventDurationHours);
            TimeSpan elapsed = utcNow - EventAnchorUtc;
            if (elapsed < TimeSpan.Zero)
            {
                return EventAnchorUtc;
            }

            long periodTicks = duration.Ticks;
            long periods = elapsed.Ticks / periodTicks;
            return EventAnchorUtc.AddTicks(periods * periodTicks);
        }

        public static DateTime GetCurrentEventEndUtc(DateTime utcNow)
        {
            return GetCurrentEventStartUtc(utcNow).AddHours(EventDurationHours);
        }

        public static string GetCurrentEventInstanceId(DateTime utcNow)
        {
            return GetCurrentEventStartUtc(utcNow).ToString("yyyyMMddHH", CultureInfo.InvariantCulture);
        }

        public static TimeSpan GetRemainingTime(DateTime utcNow)
        {
            DateTime end = GetCurrentEventEndUtc(utcNow);
            TimeSpan remaining = end - utcNow;
            return remaining.TotalSeconds > 0 ? remaining : TimeSpan.Zero;
        }

        public static string FormatRemainingTime(TimeSpan remaining)
        {
            if (remaining.TotalSeconds <= 0)
            {
                return "0d 0h 0m";
            }

            int days = remaining.Days;
            int hours = remaining.Hours;
            int minutes = remaining.Minutes;
            return $"{days}d {hours}h {minutes}m";
        }
    }
}
