using System.Globalization;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Reads the data port out of a <c>229</c> reply to <c>EPSV</c>, and the address and port
/// out of a <c>227</c> reply to <c>PASV</c>.
/// </summary>
/// <remarks>
/// The address a <c>227</c> reply names is used only under <c>--no-ftp-skip-pasv-ip</c>;
/// by default the data connection goes to the control connection's host, as curl 8.21.0
/// does (<c>--ftp-skip-pasv-ip</c> is on unless turned off).
/// </remarks>
internal static class FtpPassiveReply
{
    private const int PasvNumberCount = 6;

    private const int MaxPasvNumber = 255;

    private const int MaxPasvSignificantDigits = 3;

    /// <summary>
    /// Reads the port from an <c>EPSV</c> reply such as
    /// <c>229 Entering Extended Passive Mode (|||40000|)</c>: after the first <c>(</c>,
    /// three repeats of one delimiter, the port and the delimiter again. What follows the
    /// closing delimiter, <c>)</c> or anything else, is not read, as curl 8.21.0 does not.
    /// </summary>
    /// <param name="lastLine">The reply's last line.</param>
    /// <param name="port">
    /// The port, from 0 to 65535, when this returns <see langword="true" />; curl 8.21.0
    /// accepts 0 and dials it (measured, BL-1240).
    /// </param>
    /// <param name="failureMessage">
    /// When this returns <see langword="false" />, curl 8.21.0's exit 13 message:
    /// <see cref="FtpTransferMessages.IllegalEpsvPort" /> when the three delimiters are
    /// followed by a digit but the port is above 65535 or not closed by the delimiter,
    /// otherwise <see cref="FtpTransferMessages.WeirdEpsvReply" />.
    /// </param>
    /// <returns><see langword="true" /> when the line holds such a port.</returns>
    public static bool TryParseEpsvPort(string lastLine, out int port, out string failureMessage)
    {
        port = 0;
        failureMessage = FtpTransferMessages.WeirdEpsvReply;
        int open = lastLine.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
        {
            return false;
        }

        ReadOnlySpan<char> inside = lastLine.AsSpan(open + 1);
        if (!StartsWithThreeDelimitersAndDigit(inside))
        {
            return false;
        }

        failureMessage = FtpTransferMessages.IllegalEpsvPort;
        return TryReadDelimitedPort(inside[3..], inside[0], out port);
    }

    /// <summary>
    /// Reads the address and port from a <c>PASV</c> reply such as
    /// <c>227 Entering Passive Mode (127,0,0,1,156,64)</c>: the first four numbers as a
    /// dotted address, and the fifth number times 256 plus the sixth as the port.
    /// </summary>
    /// <param name="lastLine">The reply's last line.</param>
    /// <param name="address">
    /// The dotted address, such as <c>127.0.0.1</c>, when this returns <see langword="true" />;
    /// only <c>--no-ftp-skip-pasv-ip</c> connects to it.
    /// </param>
    /// <param name="port">
    /// The port, from 0 to 65535, when this returns <see langword="true" />; curl 8.21.0
    /// accepts 0 and dials it (measured, BL-1660).
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the line holds six comma-separated numbers, each at most
    /// 255 however many leading zeros it is written with, as curl 8.21.0 reads them
    /// (measured, BL-1660: <c>0000000000000000000001</c> is read as 1).
    /// </returns>
    public static bool TryParsePasv(string lastLine, out string address, out int port)
    {
        address = string.Empty;
        port = 0;
        Span<int> numbers = stackalloc int[PasvNumberCount];
        for (int start = 0; start < lastLine.Length; start++)
        {
            bool startsNumber = start == 0 || !char.IsAsciiDigit(lastLine[start - 1]);
            if (startsNumber && TryReadPasvNumbers(lastLine.AsSpan(start), numbers))
            {
                address = string.Create(CultureInfo.InvariantCulture, $"{numbers[0]}.{numbers[1]}.{numbers[2]}.{numbers[3]}");
                port = (numbers[4] * 256) + numbers[5];
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithThreeDelimitersAndDigit(ReadOnlySpan<char> text) =>
        text.Length >= 4 && text[1] == text[0] && text[2] == text[0] && char.IsAsciiDigit(text[3]);

    /// <summary>
    /// Reads the digits <paramref name="text" /> starts with as the port, which must be at
    /// most 65535 and be followed by <paramref name="delimiter" />.
    /// </summary>
    private static bool TryReadDelimitedPort(ReadOnlySpan<char> text, char delimiter, out int port)
    {
        port = 0;
        int digits = CountLeadingDigits(text);
        return digits < text.Length
            && text[digits] == delimiter
            && int.TryParse(text[..digits], NumberStyles.None, CultureInfo.InvariantCulture, out port)
            && port <= 65535;
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
        ReadOnlySpan<char> significant = text[..digits].TrimStart('0');
        if (digits == 0 || significant.Length > MaxPasvSignificantDigits)
        {
            return false;
        }

        number = significant.IsEmpty ? 0 : int.Parse(significant, NumberStyles.None, CultureInfo.InvariantCulture);
        text = text[digits..];
        return number <= MaxPasvNumber;
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
