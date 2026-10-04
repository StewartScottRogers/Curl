using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Core.AltSvc;

/// <summary>
/// Reads an <c>Alt-Svc</c> header value (RFC 7838) as libcurl 8.21.0's
/// <c>Curl_altsvc_parse</c> does, measured on 2026-09-29 UTC (BL-622's notes, ADR-0175).
/// </summary>
/// <remarks>
/// <para>
/// A value whose text before the first <c>;</c>, CR or LF is <c>clear</c> in any case,
/// blanks trimmed, is <c>clear</c>. Otherwise each alternative is
/// <c>alpn="[host]:port"</c> followed by its own <c>; name=value</c> parameters, and
/// alternatives are separated by commas. The ALPN is trimmed of blanks and is known only as
/// exactly <c>h1</c>, <c>h2</c> or <c>h3</c>; an alternative with any other ALPN is read and
/// skipped. The quote must follow the <c>=</c> at once. A host in brackets is an IPv6
/// address; no host means the origin's. A host longer than 2048 characters (46 in brackets),
/// a port above 65535, a missing port or a missing quote stops reading, keeping the
/// alternatives before it; <see cref="AltSvcHeader.SkipReason" /> says why when the host, IPv6
/// literal or port was bad (ADR-0409). A parameter name runs to the next <c>=</c>, so in
/// <c>h2=":1"; foo, h3=":2"</c> the <c>h3</c> alternative is read as part of a parameter and
/// lost, as it is to curl.
/// </para>
/// <para>
/// Parameter names are case-insensitive and blanks around names and values are trimmed; a
/// value may be quoted and is read as the digits it starts with. <c>ma</c> sets the maximum
/// age in seconds, <c>persist=1</c> the persist flag; no digits, or a number too big for a
/// 64-bit integer, leaves the default; other names are ignored.
/// </para>
/// </remarks>
public static class AltSvcHeaderParser
{
    /// <summary>The maximum age curl gives an alternative without a readable <c>ma</c>: 24 hours.</summary>
    public const long DefaultMaxAgeSeconds = 24 * 3600;

    /// <summary>The longest host curl reads between brackets: libcurl's <c>MAX_IPADR_LEN</c>, 46.</summary>
    private const int MaxIpv6AddressLength = 46;

    private static readonly AltSvcHeader Clear = new(true, []);

    /// <summary>Reads <paramref name="value" />.</summary>
    /// <param name="value">The header value, as received.</param>
    /// <returns>What the value says.</returns>
    public static AltSvcHeader Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (IsClear(value))
        {
            return Clear;
        }

        List<AltSvcAlternative> alternatives = [];
        AltSvcSkipReason? skipReason;
        int position = 0;
        do
        {
            if (!TryReadAlternative(value, ref position, out AltSvcAlternative? alternative, out skipReason))
            {
                break;
            }

            if (alternative is not null)
            {
                alternatives.Add(alternative);
            }

            position = SkipBlanks(value, position);
        }
        while (TryReadChar(value, ref position, ','));

        return new AltSvcHeader(false, alternatives, skipReason);
    }

    private static bool IsClear(string value)
    {
        int end = value.AsSpan().IndexOfAny(";\r\n");
        ReadOnlySpan<char> first = end < 0 ? value : value.AsSpan(0, end);
        return first.Trim(" \t").Equals("clear", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads one <c>alpn="[host]:port"</c> and its parameters; the alternative is
    /// <see langword="null" /> when its ALPN is unknown, and the skip reason says why reading
    /// failed where curl writes a line for it.
    /// </summary>
    private static bool TryReadAlternative(string value, ref int position, out AltSvcAlternative? alternative, out AltSvcSkipReason? skipReason)
    {
        alternative = null;
        skipReason = null;
        int equals = value.IndexOf('=', position);
        if (equals <= position || !value.AsSpan(equals + 1).StartsWith('"'))
        {
            return false;
        }

        AltSvcAlpn? alpn = AltSvcAlpnToken.Parse(value.AsSpan(position, equals - position).Trim(" \t"));
        position = equals + 2;
        if (!TryReadDestination(value, ref position, out string? host, out int port, out skipReason))
        {
            return false;
        }

        (long maxAge, bool persist) = ReadParameters(value, ref position);
        alternative = alpn is null ? null : new AltSvcAlternative(alpn.Value, host, port, maxAge, persist);
        return true;
    }

    private static bool TryReadDestination(string value, ref int position, out string? host, out int port, out AltSvcSkipReason? skipReason)
    {
        port = 0;
        skipReason = ReadHost(value, ref position, out host) ?? ReadPort(value, ref position, out port);
        return skipReason is null && TryReadChar(value, ref position, '"');
    }

    /// <summary>
    /// Reads the host and the <c>:</c> after it; returns why it could not, or
    /// <see langword="null" /> when it did.
    /// </summary>
    private static AltSvcSkipReason? ReadHost(string value, ref int position, out string? host)
    {
        host = null;
        if (TryReadChar(value, ref position, ':'))
        {
            return null;
        }

        if (!TryReadChar(value, ref position, '['))
        {
            return ReadHostName(value, ref position, out host);
        }

        if (!TryReadUntil(value, ref position, ']', MaxIpv6AddressLength, out host))
        {
            return AltSvcSkipReason.BadIpv6Hostname;
        }

        return TryReadChar(value, ref position, ':') ? null : AltSvcSkipReason.UnknownPortNumber;
    }

    /// <summary>
    /// Reads a host name up to the next <c>:</c> and passes it: a name longer than
    /// <see cref="AltSvcEntry.MaxHostLength" /> is a bad host name, and one no <c>:</c> follows
    /// has no port.
    /// </summary>
    private static AltSvcSkipReason? ReadHostName(string value, ref int position, out string? host)
    {
        host = null;
        int end = value.IndexOf(':', position);
        if ((end < 0 ? value.Length : end) - position > AltSvcEntry.MaxHostLength)
        {
            return AltSvcSkipReason.BadHostname;
        }

        if (end < 0)
        {
            return AltSvcSkipReason.UnknownPortNumber;
        }

        host = value[position..end];
        position = end + 1;
        return null;
    }

    /// <summary>
    /// Reads the text before the next <paramref name="terminator" /> and passes the terminator;
    /// fails when the text is empty, longer than <paramref name="maximumLength" />, or never ends.
    /// </summary>
    private static bool TryReadUntil(string value, ref int position, char terminator, int maximumLength, out string? text)
    {
        text = null;
        int end = value.IndexOf(terminator, position);
        if (end <= position || end - position > maximumLength)
        {
            return false;
        }

        text = value[position..end];
        position = end + 1;
        return true;
    }

    /// <summary>
    /// Reads the port's digits; returns <see cref="AltSvcSkipReason.UnknownPortNumber" /> when
    /// there are none or they make more than 65535.
    /// </summary>
    private static AltSvcSkipReason? ReadPort(string value, ref int position, out int port)
    {
        int end = position;
        while (end < value.Length && char.IsAsciiDigit(value[end]))
        {
            end++;
        }

        bool read = int.TryParse(value.AsSpan(position, end - position), NumberStyles.None, CultureInfo.InvariantCulture, out port)
            && port <= ushort.MaxValue;
        position = end;
        return read ? null : AltSvcSkipReason.UnknownPortNumber;
    }

    private static (long MaxAge, bool Persist) ReadParameters(string value, ref int position)
    {
        long maxAge = DefaultMaxAgeSeconds;
        bool persist = false;
        position = SkipBlanks(value, position);
        while (TryReadChar(value, ref position, ';') && TryReadParameter(value, ref position, out string name, out long? number))
        {
            maxAge = IsNamed(name, "ma") && number is not null ? number.Value : maxAge;
            persist |= IsNamed(name, "persist") && number == 1;
        }

        return (maxAge, persist);
    }

    /// <summary>
    /// Reads one <c>name=value</c> up to the next <c>;</c>, <c>,</c> or the end; fails when
    /// no <c>=</c> follows.
    /// </summary>
    private static bool TryReadParameter(string value, ref int position, out string name, out long? number)
    {
        name = string.Empty;
        number = null;
        int equals = value.IndexOf('=', position);
        if (equals < 0)
        {
            return false;
        }

        int end = value.AsSpan(equals + 1).IndexOfAny(";,");
        end = end < 0 ? value.Length : equals + 1 + end;
        name = value[position..equals];
        number = LeadingNumber(value.AsSpan(equals + 1, end - equals - 1).Trim(" \t"));
        position = end;
        return true;
    }

    private static bool IsNamed(string name, string expected) =>
        name.AsSpan().Trim(" \t").Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static long? LeadingNumber(ReadOnlySpan<char> text)
    {
        text = text.StartsWith('"') ? text[1..] : text;
        int length = 0;
        while (length < text.Length && char.IsAsciiDigit(text[length]))
        {
            length++;
        }

        return long.TryParse(text[..length], NumberStyles.None, CultureInfo.InvariantCulture, out long number) ? number : null;
    }

    private static bool TryReadChar(string value, ref int position, char expected)
    {
        if (position < value.Length && value[position] == expected)
        {
            position++;
            return true;
        }

        return false;
    }

    private static int SkipBlanks(string value, int position)
    {
        while (position < value.Length && value[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }
}
