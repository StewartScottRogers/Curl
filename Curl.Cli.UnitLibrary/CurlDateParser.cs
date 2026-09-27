using System.Collections.Frozen;
using System.Globalization;

namespace Curl.Cli;

/// <summary>
/// Reads a date the way libcurl 8.21.0's <c>curl_getdate</c> does
/// (<see href="https://curl.se/libcurl/c/curl_getdate.html"/>), for <c>-z</c>/<c>--time-cond</c>.
/// It is a port of <c>parsedate</c> in libcurl's <c>lib/parsedate.c</c>, not a general date parser:
/// it accepts what curl accepts and refuses what curl refuses.
/// </summary>
public static class CurlDateParser
{
    /// <summary>curl reads at most six words and numbers and ignores anything after them.</summary>
    private const int MaximumParts = 6;

    /// <summary>A four-digit number after <c>+</c> or <c>-</c> is a zone offset only up to <c>1400</c>.</summary>
    private const int LargestZoneOffset = 1400;

    /// <summary>The largest number curl reads in a date, eight nines.</summary>
    private const int LargestNumber = 99_999_999;

    private const int Unset = -1;

    private static readonly FrozenSet<string> WeekdayNames = FrozenSet.ToFrozenSet(
        ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>
    /// curl's zone names and how many minutes each is behind UTC, from <c>lib/parsedate.c</c>:
    /// the civil zones, then the military letters with RFC 1123's corrected signs (no <c>J</c>).
    /// </summary>
    private static readonly FrozenDictionary<string, int> ZoneMinutesBehindUtc = new Dictionary<string, int>
    {
        ["GMT"] = 0,
        ["UT"] = 0,
        ["UTC"] = 0,
        ["WET"] = 0,
        ["BST"] = -60,
        ["WAT"] = 60,
        ["AST"] = 240,
        ["ADT"] = 180,
        ["EST"] = 300,
        ["EDT"] = 240,
        ["CST"] = 360,
        ["CDT"] = 300,
        ["MST"] = 420,
        ["MDT"] = 360,
        ["PST"] = 480,
        ["PDT"] = 420,
        ["YST"] = 540,
        ["YDT"] = 480,
        ["HST"] = 600,
        ["HDT"] = 540,
        ["CAT"] = 600,
        ["AHST"] = 600,
        ["NT"] = 660,
        ["IDLW"] = 720,
        ["CET"] = -60,
        ["MET"] = -60,
        ["MEWT"] = -60,
        ["MEST"] = -120,
        ["CEST"] = -120,
        ["MESZ"] = -120,
        ["FWT"] = -60,
        ["FST"] = -120,
        ["EET"] = -120,
        ["WAST"] = -420,
        ["WADT"] = -480,
        ["CCT"] = -480,
        ["JST"] = -540,
        ["EAST"] = -600,
        ["EADT"] = -660,
        ["GST"] = -600,
        ["NZT"] = -720,
        ["NZST"] = -720,
        ["NZDT"] = -780,
        ["IDLE"] = -720,
        ["A"] = 60,
        ["B"] = 120,
        ["C"] = 180,
        ["D"] = 240,
        ["E"] = 300,
        ["F"] = 360,
        ["G"] = 420,
        ["H"] = 480,
        ["I"] = 540,
        ["K"] = 600,
        ["L"] = 660,
        ["M"] = 720,
        ["N"] = -60,
        ["O"] = -120,
        ["P"] = -180,
        ["Q"] = -240,
        ["R"] = -300,
        ["S"] = -360,
        ["T"] = -420,
        ["U"] = -480,
        ["V"] = -540,
        ["W"] = -600,
        ["X"] = -660,
        ["Y"] = -720,
        ["Z"] = 0,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly int[] DaysBeforeMonth = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];

    /// <summary>Reads <paramref name="text"/> as a date, as <c>curl_getdate</c> does.</summary>
    /// <remarks>
    /// <para>
    /// The text is read as at most six parts, separated by any run of characters that are not
    /// ASCII letters or digits; anything after the sixth part is ignored. Each part is one of:
    /// </para>
    /// <list type="bullet">
    /// <item>a weekday name, three letters or in full (<c>Sun</c>, <c>Sunday</c>), any case, which is
    /// read and ignored;</item>
    /// <item>a three-letter month name (<c>Nov</c>), any case;</item>
    /// <item>a zone name in capitals (<c>GMT</c>, <c>UTC</c>, <c>EST</c>, <c>CEST</c>, … and the military
    /// letters <c>A</c>–<c>Z</c> except <c>J</c>); <c>gmt</c> is not a zone;</item>
    /// <item>a time, <c>hh:mm:ss</c> or <c>hh:mm</c>, each field one or two digits with nothing between
    /// them and the colons, the hour below 24, the minute below 60 and the second at most 60; the first
    /// time read is the time, and a later one is read as numbers;</item>
    /// <item>a zone offset, four digits no larger than <c>1400</c> right after <c>+</c> or <c>-</c>
    /// (<c>+0100</c>);</item>
    /// <item>eight digits before any day, month or year, read as <c>yyyyMMdd</c>
    /// (<c>20300101</c>);</item>
    /// <item>any other number: a day of the month when it is 1 to 31 and no day has been read, else a
    /// year when none has been read, a year below 100 being 1971–2070 (<c>94</c> is 1994,
    /// <c>30</c> is 2030).</item>
    /// </list>
    /// <para>
    /// So RFC 1123 (<c>Sun, 06 Nov 1994 08:49:37 GMT</c>), RFC 850
    /// (<c>Sunday, 06-Nov-94 08:49:37 GMT</c>), ANSI C <c>asctime</c>
    /// (<c>Sun Nov  6 08:49:37 1994</c>), <c>1 Jan 2030</c>, <c>Jan 1 2030</c>, <c>2030 Jan 1</c> and
    /// <c>20300101 12:00 +0100</c> are all read. A day, a month and a year are required; a missing
    /// time is midnight and a missing zone is UTC. A day above 31, an hour above 23, a minute above
    /// 59 or a second above 60 is refused, but a day past the end of its month rolls into the next
    /// month (<c>31 Feb 2030</c> is 3 March 2030), as curl's arithmetic does. A number larger than
    /// 99999999 is refused. The instant one second
    /// before the Unix epoch reads as the epoch, as <c>curl_getdate</c> returns it.
    /// </para>
    /// <para>
    /// An instant after the year 9999, which curl computes in a 64-bit <c>time_t</c>, is read as the
    /// last whole second a <see cref="DateTimeOffset"/> holds, 9999-12-31 23:59:59 UTC, which compares
    /// with any file time the same way curl's instant does (ADR-0073); an instant before year 1 is
    /// refused. Checked against the local curl 8.21.0 on 2026-09-26 by bracketing the modification
    /// time of a <c>file://</c> source around the expected instant.
    /// </para>
    /// </remarks>
    /// <param name="text">The date text, after any <c>-z</c> direction prefix has been removed.</param>
    /// <param name="value">The instant read; <see langword="default"/> when the text is not a date.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a date curl accepts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static bool TryParse(string text, out DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(text);

        DateParts parts = new();
        int position = 0;
        for (int part = 0; part < MaximumParts && position < text.Length; part++)
        {
            position = SkipSeparators(text, position);
            if (position < text.Length && !TryReadPart(text, ref position, parts))
            {
                value = default;
                return false;
            }
        }

        return parts.TryGetInstant(out value);
    }

    private static int SkipSeparators(string text, int position)
    {
        while (position < text.Length && !char.IsAsciiLetterOrDigit(text[position]))
        {
            position++;
        }

        return position;
    }

    private static bool TryReadPart(string text, ref int position, DateParts parts) =>
        char.IsAsciiLetter(text[position])
            ? parts.TryTakeName(ReadRun(text, ref position, char.IsAsciiLetter))
            : TryReadNumber(text, ref position, parts);

    private static string ReadRun(string text, ref int position, Func<char, bool> belongs)
    {
        int start = position;
        while (position < text.Length && belongs(text[position]))
        {
            position++;
        }

        return text[start..position];
    }

    private static bool TryReadNumber(string text, ref int position, DateParts parts)
    {
        if (parts.Second == Unset && TryReadTime(text, ref position, parts))
        {
            return true;
        }

        int signBefore = SignBefore(text, position);
        string digits = ReadRun(text, ref position, char.IsAsciiDigit);
        return TryReadInteger(digits, out int number) && parts.TryTakeNumber(number, digits.Length, signBefore);
    }

    /// <summary>1 for a <c>+</c> just before <paramref name="position"/>, -1 for a <c>-</c>, otherwise 0.</summary>
    private static int SignBefore(string text, int position) =>
        position == 0 ? 0 : text[position - 1] switch
        {
            '+' => 1,
            '-' => -1,
            _ => 0,
        };

    private static bool TryReadInteger(string digits, out int number) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number <= LargestNumber;

    /// <summary>
    /// Reads <c>hh:mm:ss</c> or <c>hh:mm</c>, each field one or two digits, as libcurl's
    /// <c>match_time</c> does: the hour below 24, the minute below 60 and the second at most 60, or
    /// it is not a time at all. Moves <paramref name="position"/> only when it is one.
    /// </summary>
    private static bool TryReadTime(string text, ref int position, DateParts parts)
    {
        int next = position;
        int hour = ReadOneOrTwoDigits(text, ref next);
        if (hour > 23 || !TryReadTimeField(text, ref next, out int minute) || minute > 59)
        {
            return false;
        }

        int second = 0;
        if (TryReadTimeField(text, ref next, out int readSecond))
        {
            if (readSecond > 60)
            {
                return false;
            }

            second = readSecond;
        }

        parts.Hour = hour;
        parts.Minute = minute;
        parts.Second = second;
        position = next;
        return true;
    }

    /// <summary>Reads <c>:</c> followed by one or two digits; moves <paramref name="position"/> only when they are there.</summary>
    private static bool TryReadTimeField(string text, ref int position, out int field)
    {
        bool colonThenDigit = position + 1 < text.Length && text[position] == ':' && char.IsAsciiDigit(text[position + 1]);
        field = 0;
        if (colonThenDigit)
        {
            position++;
            field = ReadOneOrTwoDigits(text, ref position);
        }

        return colonThenDigit;
    }

    /// <summary>Reads the digit at <paramref name="position"/> and the one after it, when there is one.</summary>
    private static int ReadOneOrTwoDigits(string text, ref int position)
    {
        int number = text[position++] - '0';
        if (position < text.Length && char.IsAsciiDigit(text[position]))
        {
            number = (number * 10) + (text[position++] - '0');
        }

        return number;
    }

    /// <summary>
    /// The fields read so far, each <see cref="Unset"/> until read, as <c>parsedate</c> keeps them.
    /// </summary>
    private sealed class DateParts
    {
        private bool nextNumberIsDay = true;

        public bool WeekdayRead { get; private set; }

        public int Month { get; private set; } = Unset;

        public int Day { get; private set; } = Unset;

        public int Year { get; private set; } = Unset;

        public int Hour { get; set; } = Unset;

        public int Minute { get; set; } = Unset;

        public int Second { get; set; } = Unset;

        /// <summary>Seconds to add to the local reading to reach UTC; <see cref="Unset"/> when no zone was read.</summary>
        public int ZoneSecondsBehindUtc { get; private set; } = Unset;

        /// <summary>Takes a word as the first unset of weekday, month and zone it names, as curl does.</summary>
        public bool TryTakeName(string name) => TryTakeWeekday(name) || TryTakeMonth(name) || TryTakeZoneName(name);

        /// <summary>Takes a number that is not a time, in curl's order: zone offset, <c>yyyyMMdd</c>, day, year.</summary>
        /// <param name="number">The number.</param>
        /// <param name="digitCount">How many digits it was written with.</param>
        /// <param name="signBefore">1 when <c>+</c> came just before it, -1 for <c>-</c>, otherwise 0.</param>
        public bool TryTakeNumber(int number, int digitCount, int signBefore) =>
            TryTakeZoneOffset(number, digitCount, signBefore)
            || TryTakeCompactDate(number, digitCount)
            || TryTakeDayOrYear(number);

        private bool TryTakeWeekday(string name)
        {
            bool taken = !WeekdayRead && WeekdayNames.Contains(name);
            WeekdayRead |= taken;
            return taken;
        }

        private bool TryTakeMonth(string name)
        {
            int month = Array.FindIndex(MonthNames, monthName => string.Equals(monthName, name, StringComparison.OrdinalIgnoreCase));
            bool taken = Month == Unset && month != Unset;
            Month = taken ? month : Month;
            return taken;
        }

        private bool TryTakeZoneName(string name)
        {
            if (ZoneSecondsBehindUtc != Unset || !ZoneMinutesBehindUtc.TryGetValue(name, out int minutes))
            {
                return false;
            }

            ZoneSecondsBehindUtc = minutes * 60;
            return true;
        }

        /// <summary>
        /// Four digits no larger than <see cref="LargestZoneOffset"/> after a sign are a zone offset,
        /// <c>hhmm</c> ahead of UTC for <c>+</c> and behind it for <c>-</c>.
        /// </summary>
        private bool TryTakeZoneOffset(int number, int digitCount, int signBefore)
        {
            bool taken = ZoneSecondsBehindUtc == Unset && signBefore != 0 && digitCount == 4 && number <= LargestZoneOffset;
            if (taken)
            {
                ZoneSecondsBehindUtc = -signBefore * ((number / 100 * 60) + (number % 100)) * 60;
            }

            return taken;
        }

        private bool TryTakeCompactDate(int number, int digitCount)
        {
            if (digitCount != 8 || Year != Unset || Month != Unset || Day != Unset)
            {
                return false;
            }

            Year = number / 10000;
            Month = (number % 10000 / 100) - 1;
            Day = number % 100;
            return true;
        }

        /// <summary>
        /// A number is tried as the day while curl expects one; curl then expects a year, whether the
        /// day was taken or not, and expects a day again after a year when no day has been read.
        /// </summary>
        private bool TryTakeDayOrYear(int number)
        {
            if (nextNumberIsDay && Day == Unset)
            {
                nextNumberIsDay = false;
                if (number is > 0 and < 32)
                {
                    Day = number;
                    return true;
                }
            }

            if (nextNumberIsDay || Year != Unset)
            {
                return false;
            }

            Year = number < 100 ? number + (number > 70 ? 1900 : 2000) : number;
            nextNumberIsDay = Day == Unset;
            return true;
        }

        /// <summary>Turns the fields into an instant, as <c>parsedate</c> and <c>curl_getdate</c> finish.</summary>
        public bool TryGetInstant(out DateTimeOffset value)
        {
            if (Second == Unset)
            {
                Second = Minute = Hour = 0;
            }

            if (!HasDayMonthAndYear() || IsClearlyIllegal())
            {
                value = default;
                return false;
            }

            return TryFromCurlGetDateSeconds(SecondsSinceEpoch() + ZoneSecondsOrZero(), out value);
        }

        /// <summary>
        /// <c>curl_getdate</c> returns -1 for a failure, so it moves a real -1 to 0. An instant after
        /// the last whole second a <see cref="DateTimeOffset"/> holds reads as that second (ADR-0073);
        /// one before year 1 is refused, as curl refuses every year before 1583.
        /// </summary>
        private static bool TryFromCurlGetDateSeconds(long seconds, out DateTimeOffset value)
        {
            long curlSeconds = seconds == -1 ? 0 : seconds;
            bool representable = curlSeconds >= DateTimeOffset.MinValue.ToUnixTimeSeconds();
            value = representable ? DateTimeOffset.FromUnixTimeSeconds(Math.Min(curlSeconds, DateTimeOffset.MaxValue.ToUnixTimeSeconds())) : default;
            return representable;
        }

        private bool HasDayMonthAndYear() => Day != Unset && Month != Unset && Year != Unset;

        private int ZoneSecondsOrZero() => ZoneSecondsBehindUtc == Unset ? 0 : ZoneSecondsBehindUtc;

        private bool IsClearlyIllegal() => Day > 31 || Month > 11 || Hour > 23 || Minute > 59 || Second > 60;

        /// <summary>libcurl's <c>time2epoch</c>: no check that the day exists in its month.</summary>
        private long SecondsSinceEpoch()
        {
            int leapDays = Year - (Month <= 1 ? 1 : 0);
            leapDays = (leapDays / 4) - (leapDays / 100) + (leapDays / 400) - (1969 / 4) + (1969 / 100) - (1969 / 400);
            long days = ((long)(Year - 1970) * 365) + leapDays + DaysBeforeMonth[Month] + Day - 1;
            return (((days * 24) + Hour) * 60 + Minute) * 60 + Second;
        }
    }
}
