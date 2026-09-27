using System.Globalization;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Formats the current time for a <c>-w</c> <c>%time{format}</c> directive as the Linux
/// curl 8.21.0 does through glibc in the C locale (<see cref="WriteOutTimeDialect.Glibc"/>).
/// </summary>
/// <remarks>
/// <para>
/// curl first rewrites the format itself (<c>outtime</c> in <c>src/tool_writeout.c</c>):
/// <c>%f</c> becomes the six-digit microseconds, <c>%s</c> the Unix seconds, <c>%z</c>
/// <c>+0000</c>, <c>%Z</c> <c>UTC</c>, and <c>%%</c> stays <c>%%</c>. It hands the result
/// to <c>strftime</c> with the time as UTC and a 256-byte buffer, and prints nothing when
/// the result does not fit.
/// </para>
/// <para>
/// This class ports what glibc's <c>strftime</c> then does: the flags <c>_ - 0 ^ #</c>, a
/// field width, the <c>E</c> and <c>O</c> modifiers where glibc accepts them, and every
/// conversion it knows, with the C locale's names and <c>%c %x %X %r</c> layouts. A
/// directive glibc does not know is copied as it stands, padded to its width and upper
/// cased under <c>^</c>. <c>%s</c> that curl did not rewrite (<c>%-s</c>, …) reads the UTC
/// time as local standard time, as glibc's <c>mktime</c> does, and <c>%Z</c> that curl did
/// not rewrite is <c>GMT</c>. Measured on 2026-09-27; see ADR-0078.
/// </para>
/// </remarks>
internal static class GlibcTimeFormat
{
    private const int OutputBufferBytes = 256;
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
    private const string PadFlags = "_-0";
    private const char NoPad = '\0';
    private const char NoModifier = '\0';
    private const char NoConversion = '\0';

    /// <summary>What curl writes in place of <c>%f</c>, <c>%s</c>, <c>%z</c>, <c>%Z</c> and <c>%%</c> before <c>strftime</c> sees the format.</summary>
    private static readonly Dictionary<char, Func<DateTimeOffset, string>> CurlReplacements = new()
    {
        ['f'] = now => (now.UtcTicks % TimeSpan.TicksPerSecond / TicksPerMicrosecond).ToString("D6", CultureInfo.InvariantCulture),
        ['s'] = now => FormatNumber(now.ToUnixTimeSeconds()),
        ['z'] = _ => "+0000",
        ['Z'] = _ => "UTC",
        ['%'] = _ => "%%",
    };

    private static readonly Dictionary<char, Conversion> Conversions = new()
    {
        ['a'] = new("EO", (directive, moment) => Name(directive, moment.Format("ddd"))),
        ['A'] = new("EO", (directive, moment) => Name(directive, moment.Format("dddd"))),
        ['b'] = new("E", (directive, moment) => Name(directive, moment.Format("MMM")), UppercasesWhenRejected: true),
        ['B'] = new("E", (directive, moment) => Name(directive, moment.Format("MMMM"))),
        ['c'] = new("O", (directive, moment) => Subformat(directive, moment, "%a %b %e %H:%M:%S %Y")),
        ['C'] = new(string.Empty, (directive, moment) => Number(directive, 1, moment.Utc.Year / 100)),
        ['d'] = new("E", (directive, moment) => Number(directive, 2, moment.Utc.Day)),
        ['D'] = new("EO", (directive, moment) => Subformat(directive, moment, "%m/%d/%y")),
        ['e'] = new("E", (directive, moment) => SpacePaddedNumber(directive, 2, moment.Utc.Day)),
        ['F'] = new("EO", (directive, moment) => Subformat(directive, moment, "%Y-%m-%d")),
        ['g'] = new("E", (directive, moment) => Number(directive, 2, ISOWeek.GetYear(moment.Utc) % 100)),
        ['G'] = new("E", (directive, moment) => Number(directive, 1, ISOWeek.GetYear(moment.Utc))),
        ['h'] = new("E", (directive, moment) => Name(directive, moment.Format("MMM")), UppercasesWhenRejected: true),
        ['H'] = new("E", (directive, moment) => Number(directive, 2, moment.Utc.Hour)),
        ['I'] = new("E", (directive, moment) => Number(directive, 2, moment.Hour12)),
        ['j'] = new("E", (directive, moment) => Number(directive, 3, moment.Utc.DayOfYear)),
        ['k'] = new("E", (directive, moment) => SpacePaddedNumber(directive, 2, moment.Utc.Hour)),
        ['l'] = new("E", (directive, moment) => SpacePaddedNumber(directive, 2, moment.Hour12)),
        ['m'] = new("E", (directive, moment) => Number(directive, 2, moment.Utc.Month)),
        ['M'] = new("E", (directive, moment) => Number(directive, 2, moment.Utc.Minute)),
        ['n'] = new(string.Empty, (directive, _) => Pad(directive, "\n")),
        ['p'] = new(string.Empty, (directive, moment) => LowerCasedByHash(directive, moment.Format("tt"))),
        ['P'] = new(string.Empty, (directive, moment) => Pad(directive, moment.Format("tt").ToLowerInvariant())),
        ['r'] = new(string.Empty, (directive, moment) => Subformat(directive, moment, "%I:%M:%S %p")),
        ['R'] = new(string.Empty, (directive, moment) => Subformat(directive, moment, "%H:%M")),
        ['s'] = new(string.Empty, (directive, moment) => Pad(directive, FormatNumber(moment.SecondsReadAsLocalStandardTime))),
        ['S'] = new("E", (directive, moment) => Number(directive, 2, moment.Utc.Second)),
        ['t'] = new(string.Empty, (directive, _) => Pad(directive, "\t")),
        ['T'] = new(string.Empty, (directive, moment) => Subformat(directive, moment, "%H:%M:%S")),
        ['u'] = new(string.Empty, (directive, moment) => Number(directive, 1, moment.DaysSinceMonday + 1)),
        ['U'] = new("E", (directive, moment) => Number(directive, 2, moment.WeekOfYear((int)moment.Utc.DayOfWeek))),
        ['V'] = new("E", (directive, moment) => Number(directive, 2, ISOWeek.GetWeekOfYear(moment.Utc))),
        ['w'] = new("E", (directive, moment) => Number(directive, 1, (int)moment.Utc.DayOfWeek)),
        ['W'] = new("E", (directive, moment) => Number(directive, 2, moment.WeekOfYear(moment.DaysSinceMonday))),
        ['x'] = new("O", (directive, moment) => Subformat(directive, moment, "%m/%d/%y")),
        ['X'] = new("O", (directive, moment) => Subformat(directive, moment, "%H:%M:%S")),
        ['y'] = new(string.Empty, (directive, moment) => Number(directive, 2, moment.Utc.Year % 100)),
        ['Y'] = new("O", (directive, moment) => Number(directive, 1, moment.Utc.Year)),
        ['z'] = new(string.Empty, (directive, _) => Pad(directive, "+") + Number(directive, 4, 0)),
        ['Z'] = new(string.Empty, (directive, _) => LowerCasedByHash(directive, "GMT")),
        ['%'] = new(string.Empty, (directive, _) => Pad(directive, "%")),
    };

    /// <summary>
    /// Formats the time <paramref name="timeProvider"/> reads now with <paramref name="format"/>.
    /// </summary>
    /// <param name="format">The format, without the braces.</param>
    /// <param name="timeProvider">Supplies the current UTC time and the local time zone.</param>
    /// <returns>The formatted time, or an empty string when curl would print nothing.</returns>
    public static string Format(string format, TimeProvider timeProvider)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Moment moment = new(now.UtcDateTime, timeProvider.LocalTimeZone.BaseUtcOffset);
        string output = Strftime(SubstituteAsCurlDoes(format, now), moment);
        return Encoding.UTF8.GetByteCount(output) < OutputBufferBytes ? output : string.Empty;
    }

    private static string SubstituteAsCurlDoes(string format, DateTimeOffset now)
    {
        StringBuilder substituted = new();
        for (int index = 0; index < format.Length; index++)
        {
            string? replacement = format[index] == '%' && index + 1 < format.Length ? CurlReplacement(format[index + 1], now) : null;
            if (replacement is null)
            {
                substituted.Append(format[index]);
            }
            else
            {
                substituted.Append(replacement);
                index++;
            }
        }

        return substituted.ToString();
    }

    private static string? CurlReplacement(char conversion, DateTimeOffset now)
    {
        return CurlReplacements.TryGetValue(conversion, out Func<DateTimeOffset, string>? replacement) ? replacement(now) : null;
    }

    private static string Strftime(string format, Moment moment)
    {
        StringBuilder output = new();
        int position = 0;
        while (position < format.Length)
        {
            if (format[position] == '%')
            {
                position = AppendDirective(format, position, moment, output);
            }
            else
            {
                output.Append(format[position]);
                position++;
            }
        }

        return output.ToString();
    }

    private static int AppendDirective(string format, int start, Moment moment, StringBuilder output)
    {
        int position = start + 1;
        Flags flags = ReadFlags(format, ref position);
        int width = ReadWidth(format, ref position);
        char modifier = ReadModifier(format, ref position);

        // A format that ends inside a directive leaves no conversion, so glibc copies the
        // directive as it stands, as it does one it does not know.
        int end = Math.Min(position + 1, format.Length);
        char conversion = position < format.Length ? format[position] : NoConversion;
        output.Append(Render(new Directive(format[start..end], flags, width, modifier, conversion), moment));
        return end;
    }

    private static Flags ReadFlags(string format, ref int position)
    {
        Flags flags = new(NoPad, Uppercase: false, ChangeCase: false);
        while (position < format.Length && TryApplyFlag(format[position], ref flags))
        {
            position++;
        }

        return flags;
    }

    private static bool TryApplyFlag(char flag, ref Flags flags)
    {
        if (PadFlags.Contains(flag, StringComparison.Ordinal))
        {
            flags = flags with { Pad = flag };
        }
        else if (flag == '^')
        {
            flags = flags with { Uppercase = true };
        }
        else if (flag == '#')
        {
            flags = flags with { ChangeCase = true };
        }
        else
        {
            return false;
        }

        return true;
    }

    private static int ReadWidth(string format, ref int position)
    {
        // Any width from 256 up makes the result too long for curl's buffer, so it need
        // not be held exactly.
        int width = 0;
        for (; position < format.Length && char.IsAsciiDigit(format[position]); position++)
        {
            width = Math.Min((width * 10) + (format[position] - '0'), OutputBufferBytes);
        }

        return width;
    }

    private static char ReadModifier(string format, ref int position)
    {
        if (position < format.Length && format[position] is 'E' or 'O')
        {
            return format[position++];
        }

        return NoModifier;
    }

    private static string Render(Directive directive, Moment moment)
    {
        if (!Conversions.TryGetValue(directive.Conversion, out Conversion? conversion))
        {
            return Rejected(directive, directive.Flags.Uppercase);
        }

        if (conversion.RejectedModifiers.Contains(directive.Modifier, StringComparison.Ordinal))
        {
            return Rejected(directive, directive.Flags.Uppercase || (conversion.UppercasesWhenRejected && directive.Flags.ChangeCase));
        }

        return conversion.Render(directive, moment);
    }

    private static string Rejected(Directive directive, bool uppercase)
    {
        return Pad(directive, uppercase ? ToAsciiUpper(directive.Text) : directive.Text);
    }

    /// <summary>glibc's <c>cpy</c>: the text right-aligned in the width, after zeros under <c>0</c> and spaces otherwise.</summary>
    private static string Pad(Directive directive, string text)
    {
        return text.PadLeft(directive.Width, directive.Flags.Pad == '0' ? '0' : ' ');
    }

    private static string Name(Directive directive, string name)
    {
        return Pad(directive, directive.Flags.Uppercase || directive.Flags.ChangeCase ? ToAsciiUpper(name) : name);
    }

    /// <summary>For <c>%p</c> and <c>%Z</c>, <c>#</c> means lower case, and wins over <c>^</c>.</summary>
    private static string LowerCasedByHash(Directive directive, string text)
    {
        if (directive.Flags.ChangeCase)
        {
            return Pad(directive, text.ToLowerInvariant());
        }

        return Pad(directive, directive.Flags.Uppercase ? ToAsciiUpper(text) : text);
    }

    private static string Subformat(Directive directive, Moment moment, string subformat)
    {
        string text = Pad(directive, Strftime(subformat, moment));
        return directive.Flags.Uppercase ? ToAsciiUpper(text) : text;
    }

    /// <summary>
    /// glibc's <c>DO_NUMBER</c>: at least <paramref name="minimumDigits"/> digits, or the
    /// width if wider, filled with zeros, or with spaces under <c>_</c>; <c>-</c> drops the
    /// fill and pads the bare number to the width with spaces.
    /// </summary>
    private static string Number(Directive directive, int minimumDigits, long value)
    {
        string digits = FormatNumber(value);
        return directive.Flags.Pad switch
        {
            '-' => digits.PadLeft(directive.Width, ' '),
            '_' => digits.PadLeft(Math.Max(minimumDigits, directive.Width), ' '),
            _ => digits.PadLeft(Math.Max(minimumDigits, directive.Width), '0'),
        };
    }

    /// <summary>glibc's <c>DO_NUMBER_SPACEPAD</c>: fills with spaces unless <c>0</c> or <c>-</c> says otherwise.</summary>
    private static string SpacePaddedNumber(Directive directive, int minimumDigits, long value)
    {
        Directive spacePadded = directive.Flags.Pad is '0' or '-' ? directive : directive with { Flags = directive.Flags with { Pad = '_' } };
        return Number(spacePadded, minimumDigits, value);
    }

    private static string FormatNumber(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string ToAsciiUpper(string text)
    {
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                span[index] = char.IsAsciiLetterLower(source[index]) ? (char)(source[index] - ('a' - 'A')) : source[index];
            }
        });
    }

    /// <summary>The <c>_ - 0</c> fill, the <c>^</c> flag and the <c>#</c> flag of one directive.</summary>
    private readonly record struct Flags(char Pad, bool Uppercase, bool ChangeCase);

    /// <summary>One <c>%…</c> directive: its text as written, flags, width, modifier and conversion.</summary>
    private sealed record Directive(string Text, Flags Flags, int Width, char Modifier, char Conversion);

    /// <summary>
    /// One conversion glibc knows: the modifiers it rejects, how it renders, and whether
    /// <c>#</c> upper cases the directive even when a modifier is rejected (<c>%b</c> and
    /// <c>%h</c> apply <c>#</c> before they look at the modifier).
    /// </summary>
    private sealed record Conversion(string RejectedModifiers, Func<Directive, Moment, string> Render, bool UppercasesWhenRejected = false);

    /// <summary>The instant being formatted, as UTC, and the local time zone's standard offset.</summary>
    private readonly struct Moment(DateTime utc, TimeSpan localStandardOffset)
    {
        public DateTime Utc { get; } = utc;

        public int Hour12 => Utc.Hour % 12 == 0 ? 12 : Utc.Hour % 12;

        public int DaysSinceMonday => ((int)Utc.DayOfWeek + 6) % 7;

        /// <summary>What glibc's <c>mktime</c> makes of the UTC fields read as local standard time.</summary>
        public long SecondsReadAsLocalStandardTime =>
            new DateTimeOffset(Utc, TimeSpan.Zero).ToUnixTimeSeconds() - (long)localStandardOffset.TotalSeconds;

        public string Format(string layout)
        {
            return Utc.ToString(layout, CultureInfo.InvariantCulture);
        }

        public int WeekOfYear(int daysSinceWeekStart)
        {
            return (Utc.DayOfYear - 1 + 7 - daysSinceWeekStart) / 7;
        }
    }
}
