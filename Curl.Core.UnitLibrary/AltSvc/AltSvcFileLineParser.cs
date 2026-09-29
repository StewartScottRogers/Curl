using System.Globalization;

namespace Curl.Core.AltSvc;

/// <summary>
/// Reads one line of curl's alt-svc file as libcurl 8.21.0's <c>altsvc_add</c> does,
/// measured on 2026-09-29 UTC (BL-622's notes, ADR-0174).
/// </summary>
/// <remarks>
/// A line is <c>srcalpn srchost srcport dstalpn dsthost dstport "yyyyMMdd HH:mm:ss" persist
/// priority</c>, fields separated by exactly one space, ending at the end of the text or at a
/// CR. Blanks before the first field are skipped. Each ALPN is exactly <c>h1</c>, <c>h2</c> or
/// <c>h3</c>; ports are 0 to 65535, <c>persist</c> 0 or 1 and <c>priority</c> 0; the expiry is
/// UTC; a host is at most 2048 characters. Anything else - a comment, an empty line, a second
/// space, a tab, an unquoted or otherwise written date, a trailing space - is not an entry.
/// </remarks>
public static class AltSvcFileLineParser
{
    private const string DateFormat = "yyyyMMdd HH:mm:ss";

    private const int FieldCount = 10;

    /// <summary>Reads <paramref name="line" />.</summary>
    /// <param name="line">One line of the file, without its LF.</param>
    /// <returns>The entry the line holds, or <see langword="null" /> when it holds none.</returns>
    public static AltSvcEntry? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        ReadOnlySpan<char> text = line.AsSpan().TrimStart(" \t");
        int carriageReturn = text.IndexOf('\r');
        string[] fields = (carriageReturn < 0 ? text : text[..carriageReturn]).ToString().Split(' ');
        return fields.Length == FieldCount && Array.TrueForAll(fields, IsWord) ? ReadFields(fields) : null;
    }

    private static bool IsWord(string field) =>
        field.Length is > 0 and <= AltSvcEntry.MaxHostLength && !field.Contains('\t');

    private static AltSvcEntry? ReadFields(string[] fields)
    {
        int[] numbers = [ReadNumber(fields[2], ushort.MaxValue), ReadNumber(fields[5], ushort.MaxValue), ReadNumber(fields[8], 1), ReadNumber(fields[9], 0)];
        AltSvcAlpn? sourceAlpn = AltSvcAlpnToken.Parse(fields[0]);
        AltSvcAlpn? destinationAlpn = AltSvcAlpnToken.Parse(fields[3]);
        if (Array.IndexOf(numbers, -1) >= 0 || sourceAlpn is null || destinationAlpn is null)
        {
            return null;
        }

        return TryReadDate(fields[6], fields[7], out DateTimeOffset expires)
            ? AltSvcEntry.Create(sourceAlpn.Value, fields[1], numbers[0], destinationAlpn.Value, fields[4], numbers[1], expires, numbers[2] == 1)
            : null;
    }

    private static int ReadNumber(string field, int maximum) =>
        int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number <= maximum ? number : -1;

    private static bool TryReadDate(string day, string time, out DateTimeOffset expires)
    {
        expires = default;
        return day.StartsWith('"')
            && time.EndsWith('"')
            && DateTimeOffset.TryParseExact(
                $"{day[1..]} {time[..^1]}",
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out expires);
    }
}
