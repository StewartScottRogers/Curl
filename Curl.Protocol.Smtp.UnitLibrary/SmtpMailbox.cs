using System.Globalization;
using System.Text;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Writes a mailbox from <c>--mail-from</c>, <c>--mail-rcpt</c> or <c>--mail-auth</c> the way
/// curl 8.21.0 sends it, measured with <c>Record-CurlExchange.ps1 -Smtp</c> (BL-543, BL-544,
/// BL-776).
/// </summary>
/// <remarks>
/// The address is taken as curl received it in its argv (<see cref="SmtpCommandLineText" />),
/// one leading <c>&lt;</c> and one trailing <c>&gt;</c> are taken off, and the part after the
/// first <c>@</c> is converted to an IDNA A-label with the BCL's <see cref="IdnMapping" />,
/// sent as received when it is ASCII or has none: <c>c@dü.de</c> goes out as
/// <c>c@xn--d-eha.de</c>. The local part is sent as its argv bytes: <c>ö</c> is <c>F6</c> on
/// Windows-1252 and <c>C3 B6</c> in UTF-8.
/// </remarks>
internal static class SmtpMailbox
{
    /// <summary>
    /// Writes <paramref name="address" /> bare, as <c>VRFY</c> sends it.
    /// </summary>
    /// <param name="address">The address as given on the command line.</param>
    /// <param name="commandLineText">The argv encoding of the platform being matched.</param>
    /// <returns>The address without its brackets and with its host as an A-label, one character per byte.</returns>
    public static string Bare(string address, SmtpCommandLineText commandLineText)
    {
        string received = commandLineText.AsReceived(address);
        string bare = received.StartsWith('<') ? received[1..] : received;
        bare = bare.EndsWith('>') ? bare[..^1] : bare;
        int at = bare.IndexOf('@', StringComparison.Ordinal);
        return commandLineText.ToWire(at < 0 ? bare : bare[..at] + "@" + ToAsciiHost(bare[(at + 1)..]));
    }

    /// <summary>
    /// Writes <paramref name="address" /> in angle brackets, as <c>MAIL FROM</c>,
    /// <c>AUTH=</c> and <c>RCPT TO</c> send it; <see langword="null" /> is <c>&lt;&gt;</c>.
    /// </summary>
    /// <param name="address">The address as given on the command line, or <see langword="null" />.</param>
    /// <param name="commandLineText">The argv encoding of the platform being matched.</param>
    /// <returns>The bracketed address, one character per byte.</returns>
    public static string Bracketed(string? address, SmtpCommandLineText commandLineText) =>
        "<" + Bare(address ?? string.Empty, commandLineText) + ">";

    /// <summary>
    /// Whether <paramref name="address" />'s argv bytes hold one outside ASCII, so that a
    /// server advertising <c>SMTPUTF8</c> is told the command needs it. A character the
    /// Windows code page best-fits to ASCII, such as <c>ł</c>, does not (measured).
    /// </summary>
    /// <param name="address">The address as given, or <see langword="null" />.</param>
    /// <param name="commandLineText">The argv encoding of the platform being matched.</param>
    /// <returns><see langword="true" /> when some byte is not ASCII.</returns>
    public static bool NeedsSmtpUtf8(string? address, SmtpCommandLineText commandLineText) =>
        address is not null && !Ascii.IsValid(commandLineText.ToWire(address));

    /// <summary>The host part as an IDNA A-label, or as given when it is ASCII or cannot be converted.</summary>
    private static string ToAsciiHost(string host)
    {
        try
        {
            return Ascii.IsValid(host) ? host : new IdnMapping().GetAscii(host);
        }
        catch (ArgumentException)
        {
            return host;
        }
    }
}
