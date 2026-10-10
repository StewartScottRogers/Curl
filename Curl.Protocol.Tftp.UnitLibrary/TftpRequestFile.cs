using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// The file a read or write request names and the transfer mode it asks for, taken from
/// the URL path as curl 8.21.0 takes them, and the checks curl makes before it sends the
/// request.
/// </summary>
/// <remarks>
/// <para>
/// curl's <c>tftp_setup_connection</c> cuts a trailing <c>;mode=netascii</c> (ASCII mode)
/// or <c>;mode=octet</c> (binary mode, even under <c>-B</c>) off the path; without one,
/// <c>-B</c>/<c>--use-ascii</c> decides. Its <c>tftp_send_first</c> then percent-decodes
/// the name to raw bytes, refuses a decoded NUL with exit 3, refuses a name and mode
/// longer than the 512-byte packet allows with exit 71 <c>TFTP filename too long</c>, and
/// options that no longer fit with exit 71 <c>TFTP buffer too small for options</c>.
/// </para>
/// </remarks>
internal sealed class TftpRequestFile
{
    /// <summary>The longest request curl 8.21.0 sends, its 512-byte default block size.</summary>
    internal const int MaximumRequestLength = 512;

    /// <summary>What curl 8.21.0 reports for a file name too long for the request.</summary>
    internal const string FileNameTooLongMessage = "TFTP filename too long";

    /// <summary>What curl 8.21.0 reports for options that no longer fit the request.</summary>
    internal const string OptionsDoNotFitMessage = "TFTP buffer too small for options";

    /// <summary>What curl 8.21.0 reports for a file name that decodes to a NUL byte.</summary>
    internal const string MalformedUrlMessage = "URL using bad/illegal format or missing URL";

    private const string NetasciiMode = "netascii";
    private const string OctetMode = "octet";
    private const string NetasciiSuffix = ";mode=netascii";
    private const string OctetSuffix = ";mode=octet";

    private TftpRequestFile(string encodedName, string mode)
    {
        EncodedName = encodedName;
        Mode = mode;
    }

    /// <summary>Gets the file name as the URL writes it, percent-encoded, without the path's separator slash or mode suffix.</summary>
    internal string EncodedName { get; }

    /// <summary>Gets the transfer mode the request asks for: <c>netascii</c> or <c>octet</c>.</summary>
    internal string Mode { get; }

    /// <summary>Gets the file name decoded for the diagnostic log.</summary>
    internal string LoggedName => Uri.UnescapeDataString(EncodedName);

    /// <summary>
    /// Takes the file name and mode from a <c>tftp://</c> URL's path.
    /// </summary>
    /// <param name="absolutePath">The URL's path, percent-encoded, starting with <c>/</c>.</param>
    /// <param name="useAscii">Whether <c>-B</c>/<c>--use-ascii</c> was given.</param>
    /// <returns>The file the request names.</returns>
    internal static TftpRequestFile FromUrlPath(string absolutePath, bool useAscii)
    {
        // RFC 3617: only the one separator slash is not part of the name, so curl's
        // tftp_send_first skips the path's first character and keeps any further slash.
        string name = absolutePath[1..];
        if (name.EndsWith(NetasciiSuffix, StringComparison.Ordinal))
        {
            return new TftpRequestFile(name[..^NetasciiSuffix.Length], NetasciiMode);
        }

        if (name.EndsWith(OctetSuffix, StringComparison.Ordinal))
        {
            return new TftpRequestFile(name[..^OctetSuffix.Length], OctetMode);
        }

        return new TftpRequestFile(name, useAscii ? NetasciiMode : OctetMode);
    }

    /// <summary>
    /// Builds the request from the decoded file name, or gives the failure curl 8.21.0 ends
    /// with before sending it, reporting curl's <c>-v</c> line for an exit 71.
    /// </summary>
    /// <param name="buildRequest">Builds the whole datagram from the decoded name and the mode.</param>
    /// <param name="events">Where the <c>-v</c> line of an exit 71 is reported.</param>
    /// <param name="request">The datagram, or empty when the request is refused.</param>
    /// <returns>The failure when the request is refused, otherwise <see langword="null" />.</returns>
    internal TransferResult? TryBuildRequest(
        Func<byte[], string, byte[]> buildRequest, TftpTransferEvents events, out byte[] request)
    {
        request = [];
        if (RefuseUnsendableName(events, out byte[] name) is { } refused)
        {
            return refused;
        }

        byte[] built = buildRequest(name, Mode);
        if (built.Length > MaximumRequestLength)
        {
            return Refuse(OptionsDoNotFitMessage, events);
        }

        request = built;
        return null;
    }

    /// <summary>
    /// Gives the failure curl 8.21.0 ends with for a file name it cannot send whatever the
    /// options: exit 3 for a decoded NUL, exit 71 <c>TFTP filename too long</c> (reporting
    /// curl's <c>-v</c> line) for a name and mode too long for the request.
    /// </summary>
    /// <param name="events">Where the <c>-v</c> line of an exit 71 is reported.</param>
    /// <returns>The failure when the name is refused, otherwise <see langword="null" />.</returns>
    internal TransferResult? RefuseUnsendableName(TftpTransferEvents events) => RefuseUnsendableName(events, out _);

    // Decodes the name, or refuses it as curl's tftp_send_first does before it adds the options.
    private TransferResult? RefuseUnsendableName(TftpTransferEvents events, out byte[] name)
    {
        byte[]? decoded = DecodeName();
        name = decoded ?? [];
        if (decoded is null)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, MalformedUrlMessage);
        }

        return name.Length + Mode.Length + 4 > MaximumRequestLength ? Refuse(FileNameTooLongMessage, events) : null;
    }

    // Reports curl's -v line for the refusal and fails with exit 71.
    private static TransferResult Refuse(string message, TftpTransferEvents events)
    {
        events.Refused(message);
        return TransferResult.Failure(CurlExitCode.TftpIllegal, message);
    }

    // Percent-decodes the name to bytes as curl's Curl_urldecode does: a % followed by two
    // hex digits is that byte, anything else is kept as written. Null for a decoded NUL.
    private byte[]? DecodeName()
    {
        byte[] encoded = Encoding.UTF8.GetBytes(EncodedName);
        var decoded = new byte[encoded.Length];
        int length = 0;
        for (int index = 0; index < encoded.Length; index++)
        {
            byte value = encoded[index];
            if (IsEscape(encoded, index))
            {
                value = (byte)((HexValue(encoded[index + 1]) << 4) | HexValue(encoded[index + 2]));
                index += 2;
            }

            decoded[length++] = value;
        }

        return Array.IndexOf(decoded, (byte)0, 0, length) >= 0 ? null : decoded[..length];
    }

    // Whether a % followed by two hex digits starts at index.
    private static bool IsEscape(byte[] encoded, int index) =>
        encoded[index] == '%' && index + 2 < encoded.Length && IsHexDigit(encoded[index + 1]) && IsHexDigit(encoded[index + 2]);

    private static bool IsHexDigit(byte value) => char.IsAsciiHexDigit((char)value);

    private static int HexValue(byte value) => value <= '9' ? value - '0' : (value | 0x20) - 'a' + 10;
}
