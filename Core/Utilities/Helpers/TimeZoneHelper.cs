using System;

namespace Core.Utilities.Helpers
{
    /// <summary>
    /// Centralized timezone helper to avoid repeated TimeZoneInfo lookups
    /// </summary>
    public static class TimeZoneHelper
    {
        private static readonly Lazy<TimeZoneInfo> _turkeyTimeZone = new Lazy<TimeZoneInfo>(() =>
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
            }
        });

        public static TimeZoneInfo TurkeyTimeZone => _turkeyTimeZone.Value;

        public static DateTime ToTurkeyTime(DateTime utcTime)
        {
            return TimeZoneInfo.ConvertTime(utcTime, TimeZoneInfo.Utc, TurkeyTimeZone);
        }

        public static DateTime ToUtcTime(DateTime turkeyTime)
        {
            return TimeZoneInfo.ConvertTime(turkeyTime, TurkeyTimeZone, TimeZoneInfo.Utc);
        }

        /// <summary>TR gün başlangıcının UTC karşılığı (free tier günlük limitler için).</summary>
        public static DateTime GetTurkeyDayStartUtc(DateTime? utcNow = null)
        {
            var utc = utcNow ?? DateTime.UtcNow;
            var trNow = ToTurkeyTime(utc);
            var trMidnight = new DateTime(trNow.Year, trNow.Month, trNow.Day, 0, 0, 0, DateTimeKind.Unspecified);
            return ToUtcTime(trMidnight);
        }
    }
}

