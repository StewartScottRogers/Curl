namespace Curl.Cli;

/// <summary>
/// The local ports a <c>--local-port &lt;num&gt;[-num]</c> value names: every port from <see cref="First"/> to
/// <see cref="Last"/>, both included, which curl hands libcurl as <c>CURLOPT_LOCALPORT</c> (the first) and
/// <c>CURLOPT_LOCALPORTRANGE</c> (<see cref="Count"/>). A single number is a range of one port.
/// </summary>
/// <param name="First">The first port tried, 0 to 65535.</param>
/// <param name="Last">The last port tried, <see cref="First"/> to 65535.</param>
public readonly record struct LocalPortRange(int First, int Last)
{
    private const int MaximumPort = 65535;

    /// <summary>How many ports the range holds: <see cref="Last"/> - <see cref="First"/> + 1.</summary>
    public int Count => Last - First + 1;

    /// <summary>
    /// Reads a <c>--local-port</c> value as curl 8.21.0 does (measured on Windows, 2026-09-28, BL-599 Notes):
    /// one or more decimal digits, optionally followed by one blank at most, a <c>-</c>, one blank at most and
    /// one or more digits. Leading zeros are allowed; any other character, a sign, a port past 65535 or a last
    /// port below the first is refused.
    /// </summary>
    /// <param name="value">The value as given.</param>
    /// <param name="range">The range read, or the default when the value is refused.</param>
    /// <returns><see langword="false"/> when curl refuses the value as badly used.</returns>
    public static bool TryParse(string value, out LocalPortRange range)
    {
        ArgumentNullException.ThrowIfNull(value);

        range = default;
        int position = 0;
        if (!TryReadPort(value, ref position, out int first))
        {
            return false;
        }

        int last = first;
        if ((position < value.Length && !TryReadLastPort(value, position, out last)) || last < first)
        {
            return false;
        }

        range = new LocalPortRange(first, last);
        return true;
    }

    /// <summary>Reads <c>[blank]-[blank]digits</c> from <paramref name="position"/> to the end of <paramref name="value"/>.</summary>
    private static bool TryReadLastPort(string value, int position, out int last)
    {
        last = 0;
        SkipOneBlank(value, ref position);
        if (position == value.Length || value[position] != '-')
        {
            return false;
        }

        position++;
        SkipOneBlank(value, ref position);
        return TryReadPort(value, ref position, out last) && position == value.Length;
    }

    /// <summary>Moves <paramref name="position"/> past one space or tab, when one is there.</summary>
    private static void SkipOneBlank(string value, ref int position)
    {
        if (position < value.Length && value[position] is ' ' or '\t')
        {
            position++;
        }
    }

    /// <summary>Reads one or more digits from <paramref name="position"/> as a port no greater than 65535.</summary>
    private static bool TryReadPort(string value, ref int position, out int port)
    {
        int start = position;
        port = 0;
        while (position < value.Length && char.IsAsciiDigit(value[position]))
        {
            // Past the maximum the value stays past it, so a long run of digits cannot overflow.
            port = Math.Min(port * 10 + (value[position] - '0'), MaximumPort + 1);
            position++;
        }

        return position > start && port <= MaximumPort;
    }
}
