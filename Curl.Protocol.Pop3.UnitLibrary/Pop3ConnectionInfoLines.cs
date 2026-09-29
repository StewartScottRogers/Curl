using System.Globalization;

namespace Curl.Protocol.Pop3;

/// <summary>
/// The <c>-v</c> information lines curl 8.21.0 ends a POP3 transfer with, each measured with
/// <c>Record-CurlExchange.ps1 -Pop3</c> (BL-552).
/// </summary>
internal static class Pop3ConnectionInfoLines
{
    /// <summary>
    /// Formats the line for a transfer that succeeded, whose connection curl keeps until it
    /// sends <c>QUIT</c> on exit.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <param name="host">The host the connection was opened to.</param>
    /// <param name="port">The port the connection was opened to.</param>
    /// <returns>The line, such as <c>Connection #0 to host 127.0.0.1:18110 left intact</c>.</returns>
    internal static string LeftIntact(long connectionNumber, string host, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection #{connectionNumber} to host {host}:{port} left intact");

    /// <summary>
    /// Formats the line for a transfer that failed once the session was logged in.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDown(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}");

    /// <summary>
    /// Formats the line for a transfer that failed before the session was logged in.
    /// </summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string Closing(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");
}
