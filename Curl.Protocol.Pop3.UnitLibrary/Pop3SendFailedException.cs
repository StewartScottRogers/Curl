using System.Net.Sockets;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Ends a POP3 transfer when a command cannot be written to the connection, which curl 8.21.0
/// ends at once with exit 55 (BL-1252): <c>Send failure: Connection was reset</c> for a reset,
/// the socket filter's <c>failf</c>, and <c>Failed sending data to the peer</c>,
/// <c>CURLE_SEND_ERROR</c>'s own text, for any other failure.
/// </summary>
/// <param name="failure">What the connection threw.</param>
internal sealed class Pop3SendFailedException(IOException failure)
    : Exception(IsReset(failure) ? Pop3SessionMessages.SendConnectionReset : Pop3SessionMessages.SendFailed, failure)
{
    private static bool IsReset(IOException exception) =>
        exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset };
}
