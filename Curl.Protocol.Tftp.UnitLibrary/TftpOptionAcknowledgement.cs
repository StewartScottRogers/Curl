using System.Globalization;
using System.Text;

namespace Curl.Protocol.Tftp;

/// <summary>
/// An option acknowledgement read as curl 8.21.0's <c>tftp_parse_option_ack</c> reads it
/// (<c>lib/tftp.c</c> lines 259-330 at tag <c>curl-8_21_0</c>): option by option, stopping
/// at the first one it rejects.
/// </summary>
/// <param name="Options">
/// The options read, in order, up to and including the one rejected; an option without
/// its terminators is not among them.
/// </param>
/// <param name="BlockSize">
/// The block size the transfer goes on with: the last <c>blksize</c> granted, 512 when
/// there is none.
/// </param>
/// <param name="Failure">
/// curl's exit 71 (<c>CURLE_TFTP_ILLEGAL</c>) message for the option it rejected, or
/// <see langword="null" /> when it accepted the acknowledgement.
/// </param>
internal sealed record TftpOptionAcknowledgement(
    IReadOnlyList<TftpAcknowledgedOption> Options,
    int BlockSize,
    string? Failure)
{
    /// <summary>The message for an option or value without its terminating NUL.</summary>
    internal const string MalformedMessage = "Malformed ACK packet, rejecting";

    /// <summary>
    /// Reads an option acknowledgement's body.
    /// </summary>
    /// <param name="body">The OACK after its opcode: null-terminated name and value pairs.</param>
    /// <param name="requestedBlockSize">
    /// The <c>blksize</c> the request asked for, or 512 when it carried no options; a
    /// granted <c>blksize</c> above it is rejected.
    /// </param>
    /// <param name="isDownload">
    /// Whether the transfer is a download; an upload ignores <c>tsize</c>.
    /// </param>
    /// <returns>What curl took from the acknowledgement, or why it rejected it.</returns>
    internal static TftpOptionAcknowledgement Parse(ReadOnlySpan<byte> body, int requestedBlockSize, bool isDownload)
    {
        var options = new List<TftpAcknowledgedOption>();
        var blockSize = TftpPackets.DefaultBlockSize;
        var rest = body;
        while (!rest.IsEmpty)
        {
            if (!TrySplitOption(ref rest, out var name, out var value))
            {
                return new(options, blockSize, MalformedMessage);
            }

            var (option, failure) = ReadOption(name, value, requestedBlockSize, isDownload);
            options.Add(option);
            if (failure is not null)
            {
                return new(options, blockSize, failure);
            }

            blockSize = option.BlockSize ?? blockSize;
        }

        return new(options, blockSize, null);
    }

    /// <summary>
    /// Reads the decimal digits <c>curlx_str_number</c> reads at the start of
    /// <paramref name="value" />, ignoring whatever follows them.
    /// </summary>
    /// <param name="value">The text to read.</param>
    /// <param name="maximum">The largest number accepted.</param>
    /// <param name="number">The number read.</param>
    /// <param name="digitCount">How many characters the digits took.</param>
    /// <returns>
    /// <see langword="false" /> when <paramref name="value" /> does not start with a digit
    /// or its number is above <paramref name="maximum" />.
    /// </returns>
    internal static bool TryReadLeadingNumber(string value, long maximum, out long number, out int digitCount)
    {
        number = 0;
        digitCount = 0;
        while (digitCount < value.Length && char.IsAsciiDigit(value[digitCount]))
        {
            var digit = value[digitCount] - '0';
            if (number > (maximum - digit) / 10)
            {
                return false;
            }

            number = (number * 10) + digit;
            digitCount++;
        }

        return digitCount > 0;
    }

    /// <summary>
    /// Takes one name and value pair off the front of <paramref name="rest" />, as
    /// <c>tftp_option_get</c> does.
    /// </summary>
    /// <param name="rest">The unread options, advanced past the pair when it is whole.</param>
    /// <param name="name">The option's name.</param>
    /// <param name="value">The option's value.</param>
    /// <returns>
    /// <see langword="false" /> when the name has no NUL with something after it, or the
    /// value has no NUL.
    /// </returns>
    private static bool TrySplitOption(ref ReadOnlySpan<byte> rest, out string name, out string value)
    {
        name = value = string.Empty;
        var nameEnd = rest.IndexOf((byte)0);
        if (nameEnd < 0 || nameEnd + 1 >= rest.Length)
        {
            return false;
        }

        var afterName = rest[(nameEnd + 1)..];
        var valueEnd = afterName.IndexOf((byte)0);
        if (valueEnd < 0)
        {
            return false;
        }

        name = Encoding.UTF8.GetString(rest[..nameEnd]);
        value = Encoding.UTF8.GetString(afterName[..valueEnd]);
        rest = afterName[(valueEnd + 1)..];
        return true;
    }

    private static (TftpAcknowledgedOption Option, string? Failure) ReadOption(
        string name,
        string value,
        int requestedBlockSize,
        bool isDownload)
    {
        if (string.Equals(name, "blksize", StringComparison.OrdinalIgnoreCase))
        {
            return ReadBlockSize(name, value, requestedBlockSize);
        }

        return isDownload && string.Equals(name, "tsize", StringComparison.OrdinalIgnoreCase)
            ? ReadTransferSize(name, value)
            : (new(name, value, null, null), null);
    }

    private static (TftpAcknowledgedOption Option, string? Failure) ReadBlockSize(string name, string value, int requestedBlockSize)
    {
        string? failure = !TryReadLeadingNumber(value, TftpPackets.MaximumBlockSize, out var blockSize, out _)
            ? string.Create(CultureInfo.InvariantCulture, $"blksize is larger than max supported ({TftpPackets.MaximumBlockSize})")
            : blockSize == 0
                ? "invalid blocksize value in OACK packet"
                : blockSize < TftpPackets.MinimumBlockSize
                    ? string.Create(CultureInfo.InvariantCulture, $"blksize is smaller than min supported ({TftpPackets.MinimumBlockSize})")
                    : blockSize > requestedBlockSize
                        ? string.Create(CultureInfo.InvariantCulture, $"server requested blksize larger than allocated ({blockSize})")
                        : null;
        return (new(name, value, failure is null ? (int)blockSize : null, null), failure);
    }

    private static (TftpAcknowledgedOption Option, string? Failure) ReadTransferSize(string name, string value)
    {
        if (!TryReadLeadingNumber(value, long.MaxValue, out var transferSize, out var digitCount))
        {
            return (new(name, value, null, null), null);
        }

        return transferSize == 0
            ? (new(name, value, null, null), $"invalid tsize -:{value[digitCount..]}:- value in OACK packet")
            : (new(name, value, null, transferSize), null);
    }
}
