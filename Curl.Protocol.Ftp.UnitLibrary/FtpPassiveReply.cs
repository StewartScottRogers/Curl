using System.Globalization;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Reads the data port out of a <c>229</c> reply to <c>EPSV</c> or a <c>227</c> reply to
/// <c>PASV</c>.
/// </summary>
/// <remarks>
/// Only the port is taken. The address a <c>227</c> reply names is ignored and the data
/// connection goes to the control connection's host, as curl 8.21.0 does by default
/// (<c>--ftp-skip-pasv-ip</c> is on unless turned off).
/// </remarks>
internal static class FtpPassiveReply
{
    private const int PasvNumberCount = 6;

    private const int MaxPasvDigits = 3;

    /// <summary>
    /// Reads the port from an <c>EPSV</c> reply such as
    /// <c>229 Entering Extended Passive Mode (|||40000|)</c>: after the first <c>(</c>,
    /// three repeats of one delimiter, the port, the delimiter again and <c>)</c>.
    /// </summary>
    /// <param name="lastLine">The reply's last line.</param>
    /// <param name="port">The port, from 1 to 65535, when this returns <see langword="true" />.</param>
    /// <returns><see langword="true" /> when the line holds such a port.</returns>
    public static bool TryParseEpsvPort(string lastLine, out int port)
    {
        port = 0;
        int open = lastLine.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
        {
            return false;
        }

        ReadOnlySpan<char> inside = lastLine.AsSpan(open + 1);
        return StartsWithThreeDelimiters(inside)
            && TryReadDelimitedPort(inside[3..], inside[0], out port);
    }

    /// <summary>
    /// Reads the port from a <c>PASV</c> reply such as
    /// <c>227 Entering Passive Mode (127,0,0,1,156,64)</c>: the fifth number times 256
    /// plus the sixth.
    /// </summary>
    /// <param name="lastLine">The reply's last line.</param>
    /// <param name="port">The port, from 1 to 65535, when this returns <see langword="true" />.</param>
    /// <returns>
    /// <see langword="true" /> when the line holds six comma-separated numbers of at most
    /// three digits, each at most 255, naming a port other than 0.
    /// </returns>
    public static bool TryParsePasvPort(string lastLine, out int port)
    {
        port = 0;
        Span<int> numbers = stackalloc int[PasvNumberCount];
        for (int start = 0; start < lastLine.Length; start++)
        {
            bool startsNumber = start == 0 || !char.IsAsciiDigit(lastLine[start - 1]);
            if (startsNumber && TryReadPasvNumbers(lastLine.AsSpan(start), numbers))
            {
                port = (numbers[4] * 256) + numbers[5];
                return numbers.IndexOfAnyExceptInRange(0, 255) < 0 && port >= 1;
            }
        }

        return false;
    }

    private static bool StartsWithThreeDelimiters(ReadOnlySpan<char> text) =>
        text.Length >= 3 && text[1] == text[0] && text[2] == text[0];

    private static bool TryReadDelimitedPort(ReadOnlySpan<char> text, char delimiter, out int port)
    {
        port = 0;
        int end = text.IndexOf(delimiter);
        return end >= 0
            && text[(end + 1)..].StartsWith(')')
            && int.TryParse(text[..end], NumberStyles.None, CultureInfo.InvariantCulture, out port)
            && port is >= 1 and <= 65535;
    }

    private static bool TryReadPasvNumbers(ReadOnlySpan<char> text, Span<int> numbers)
    {
        for (int index = 0; index < numbers.Length; index++)
        {
            bool separated = index == 0 || TrySkipComma(ref text);
            if (!separated || !TryReadNumber(ref text, out numbers[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadNumber(ref ReadOnlySpan<char> text, out int number)
    {
        number = 0;
        int digits = CountLeadingDigits(text);
        if (digits is 0 or > MaxPasvDigits)
        {
            return false;
        }

        number = int.Parse(text[..digits], NumberStyles.None, CultureInfo.InvariantCulture);
        text = text[digits..];
        return true;
    }

    private static bool TrySkipComma(ref ReadOnlySpan<char> text)
    {
        if (!text.StartsWith(','))
        {
            return false;
        }

        text = text[1..];
        return true;
    }

    private static int CountLeadingDigits(ReadOnlySpan<char> text)
    {
        int count = text.IndexOfAnyExceptInRange('0', '9');
        return count < 0 ? text.Length : count;
    }
}
