using System.Globalization;

namespace Curl.Protocol.Imap;

/// <summary>
/// The <c>-v</c> information lines curl 8.21.0 writes during and at the end of an IMAP
/// transfer, each measured with <c>Record-CurlExchange.ps1 -Imap</c> (BL-559).
/// </summary>
internal static class ImapInfoLines
{
    /// <summary>The line after a <c>PREAUTH</c> greeting.</summary>
    internal const string Preauthenticated = "PREAUTH connection, already authenticated";

    /// <summary>The line once an empty <c>APPEND</c> literal has been sent.</summary>
    internal const string RequestSent = "Request completely sent off";

    /// <summary>Formats the line for a response announcing a literal of <paramref name="size" /> bytes.</summary>
    /// <param name="size">The literal's size.</param>
    /// <returns>The line, such as <c>Found 100 bytes to download</c>.</returns>
    internal static string Found(long size) =>
        string.Create(CultureInfo.InvariantCulture, $"Found {size} bytes to download");

    /// <summary>
    /// Formats the line for the part of a <c>FETCH</c> literal that arrived with its response
    /// line, once written.
    /// </summary>
    /// <param name="written">The bytes written.</param>
    /// <param name="left">The literal's bytes still to come.</param>
    /// <returns>The line, such as <c>Written 100 bytes, 0 bytes are left for transfer</c>.</returns>
    internal static string Written(long written, long left) =>
        string.Create(CultureInfo.InvariantCulture, $"Written {written} bytes, {left} bytes are left for transfer");

    /// <summary>Formats the line once an <c>APPEND</c> literal of one byte or more has been sent.</summary>
    /// <param name="bytesSent">The literal's bytes sent.</param>
    /// <returns>The line, such as <c>upload completely sent off: 21 bytes</c>.</returns>
    internal static string UploadSent(long bytesSent) =>
        string.Create(CultureInfo.InvariantCulture, $"upload completely sent off: {bytesSent} bytes");

    /// <summary>
    /// Formats the line for a transfer that succeeded, or that failed only once its literal
    /// was through, whose connection curl keeps until it sends <c>LOGOUT</c> on exit.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <param name="host">The host the connection was opened to.</param>
    /// <param name="port">The port the connection was opened to.</param>
    /// <returns>The line, such as <c>Connection #0 to host 127.0.0.1:18143 left intact</c>.</returns>
    internal static string LeftIntact(long connectionNumber, string host, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection #{connectionNumber} to host {host}:{port} left intact");

    /// <summary>Formats the line for a transfer that failed once logged in, before any literal moved.</summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDown(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}");

    /// <summary>Formats the line for a transfer that failed before logging in, or while a literal was read.</summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string Closing(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");
}
