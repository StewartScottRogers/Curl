using System.Net.Sockets;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Ends an SMTP transfer when a command or a piece of the message cannot be written to the
/// connection, which curl 8.21.0 ends at once with exit 55 (BL-1243): <c>Send failure:
/// Connection was reset</c> for a reset, the socket filter's <c>failf</c>, and <c>Failed
/// sending data to the peer</c>, <c>CURLE_SEND_ERROR</c>'s own text, for any other failure.
/// </summary>
/// <param name="failure">What the connection threw.</param>
internal sealed class SmtpSendFailedException(IOException failure)
    : Exception(IsReset(failure) ? SmtpSessionMessages.SendConnectionReset : SmtpSessionMessages.SendFailed, failure)
{
    private static bool IsReset(IOException exception) =>
        exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset };
}
