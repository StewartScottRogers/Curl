using System.Globalization;

namespace Curl.Protocol.Smtp;

/// <summary>
/// The <c>-v</c> information lines curl 8.21.0 writes for an SMTP transfer besides the
/// commands and replies, each measured with <c>Record-CurlExchange.ps1 -Smtp</c> (BL-546).
/// </summary>
internal static class SmtpConnectionInfoLines
{
    /// <summary>
    /// The line written before exit 67 when no mechanism could be used and the <c>EHLO</c>
    /// reply offered none curl knows (BL-1061).
    /// </summary>
    internal const string NoSaslMechanismOffered = "SASL: no auth mechanism was offered or recognized";

    /// <summary>
    /// The line written before exit 67 when no mechanism could be used although the
    /// <c>EHLO</c> reply offered one curl knows (BL-1061).
    /// </summary>
    internal const string NoSaslMechanismOverlap = "SASL: no overlap between offered and configured auth mechanisms";

    /// <summary>
    /// The line written before exit 67 when the only mechanisms offered and allowed are ones
    /// curl 8.21.0's Schannel build does not build in, each then named by
    /// <see cref="SaslMechanismNotBuiltIn" /> (BL-1061).
    /// </summary>
    internal const string NoSaslMechanismSelectable = "SASL: no auth mechanism offered could be selected";

    /// <summary>
    /// Formats the line naming a mechanism that was offered and allowed but is not built in,
    /// after <see cref="NoSaslMechanismSelectable" /> (BL-1061).
    /// </summary>
    /// <param name="mechanism">The mechanism, as curl names it.</param>
    /// <returns>The line, such as <c>SASL: SCRAM-SHA-256 not builtin</c>.</returns>
    internal static string SaslMechanismNotBuiltIn(string mechanism) => "SASL: " + mechanism + " not builtin";

    /// <summary>
    /// Formats the line written once the message and its end-of-data mark are sent.
    /// </summary>
    /// <param name="bytesSent">The bytes sent after <c>DATA</c>, stuffed dots and the mark included.</param>
    /// <returns>The line, such as <c>upload completely sent off: 25 bytes</c>.</returns>
    internal static string UploadSent(long bytesSent) =>
        string.Create(CultureInfo.InvariantCulture, $"upload completely sent off: {bytesSent} bytes");

    /// <summary>
    /// Formats the line for a transfer that succeeded, whose connection curl keeps until it
    /// sends <c>QUIT</c> on exit.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <param name="host">The host the connection was opened to.</param>
    /// <param name="port">The port the connection was opened to.</param>
    /// <returns>The line, such as <c>Connection #0 to host 127.0.0.1:18025 left intact</c>.</returns>
    internal static string LeftIntact(long connectionNumber, string host, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection #{connectionNumber} to host {host}:{port} left intact");

    /// <summary>
    /// Formats the line for a transfer that failed once the session was open, whose
    /// connection curl shuts down with <c>QUIT</c>.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDown(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}");

    /// <summary>
    /// Formats the line for a transfer that failed before the session was open, or lost its
    /// connection, which curl closes without <c>QUIT</c>.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string Closing(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");
}
