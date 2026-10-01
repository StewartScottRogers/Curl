using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// The reason curl 8.21.0 gives after <c>failed:</c> in its <c>connect to ... failed</c>
/// info line: its own Winsock wording on Windows, the system's message elsewhere.
/// </summary>
/// <remarks>
/// On Windows curl's <c>Curl_strerror</c> words the common Winsock errors itself and falls
/// back to the system's message; elsewhere it prints <c>strerror</c>, which is the message
/// .NET gives a <see cref="SocketException" /> there. <c>Connection refused</c> was
/// measured (BL-408), <c>Network down</c> and <c>Invalid arguments</c> too, from a Unix socket
/// connect (BL-507), <c>Address already in use</c> from a <c>--local-port</c> bind (BL-1027); the other Winsock words are curl's <c>lib/strerror.c</c> table.
/// </remarks>
public static class ConnectFailureReason
{
    private static readonly Dictionary<SocketError, string> WinsockWords = new()
    {
        [SocketError.ConnectionRefused] = "Connection refused",
        [SocketError.TimedOut] = "Timed out",
        [SocketError.NetworkUnreachable] = "Network unreachable",
        [SocketError.HostUnreachable] = "Host unreachable",
        [SocketError.NetworkDown] = "Network down",
        [SocketError.HostDown] = "Host down",
        [SocketError.AddressNotAvailable] = "Address not available",
        [SocketError.ConnectionReset] = "Connection reset",
        [SocketError.ConnectionAborted] = "Connection aborted",
        [SocketError.InvalidArgument] = "Invalid arguments",
        [SocketError.AddressAlreadyInUse] = "Address already in use",
    };

    /// <summary>
    /// Gives the reason for a failed connect as the platform's curl words it.
    /// </summary>
    /// <param name="exception">The failed connect.</param>
    /// <param name="usesWinsockWording">
    /// <see langword="true" /> for the Windows build's own words for common Winsock errors.
    /// </param>
    /// <returns>The reason, such as <c>Connection refused</c>.</returns>
    public static string Describe(SocketException exception, bool usesWinsockWording)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return usesWinsockWording && WinsockWords.TryGetValue(exception.SocketErrorCode, out var words)
            ? words
            : exception.Message;
    }
}
