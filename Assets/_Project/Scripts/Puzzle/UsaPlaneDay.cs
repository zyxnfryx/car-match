namespace CarMatch
{
    // Once-per-local-calendar-day gate, ported from Flock Five's UsaPlaneDay.
    // Today is passed in. This type does not read the clock or PlayerPrefs.
    // The stored value is Stamp's yyyy-MM-dd, written only when a flight starts.
    public static class UsaPlaneDay
    {
        public static string Stamp(System.DateTime local)
        {
            return local.Year.ToString("0000") + "-" + local.Month.ToString("00") + "-" + local.Day.ToString("00");
        }

        public static bool AlreadyFlown(string recordedDay, string today)
        {
            return !string.IsNullOrEmpty(today) && recordedDay == today;
        }

        // force is the debug fly-now override. It may start even when today is already recorded.
        public static bool MayStart(string recordedDay, string today, bool force)
        {
            if (string.IsNullOrEmpty(today)) return false;
            if (force) return true;
            return !AlreadyFlown(recordedDay, today);
        }

        // Copies today into record only when a flight starts. Otherwise record stays the stored day.
        public static bool TryBegin(string recordedDay, string today, bool force, out string record)
        {
            if (!MayStart(recordedDay, today, force))
            {
                record = recordedDay ?? "";
                return false;
            }
            record = today;
            return true;
        }
    }
}
