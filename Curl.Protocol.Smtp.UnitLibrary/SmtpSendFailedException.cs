using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Ends an SMTP transfer when a command or a piece of the message cannot be written to the
/// connection, which curl 8.21.0 ends at once with exit 55 (BL-1243, BL-1345): <c>Send failure:
/// &lt;words&gt;</c> for any socket error, the socket filter's <c>failf</c> worded by
/// <see cref="CurlSocketErrorText.SendFailure(IOException)" />, and <c>Failed sending data to the
/// peer</c>, <c>CURLE_SEND_ERROR</c>'s own text, for a failure with no socket error in it.
/// </summary>
/// <param name="failure">What the connection threw.</param>
internal sealed class SmtpSendFailedException(IOException failure)
    : Exception(CurlSocketErrorText.SendFailure(failure) ?? SmtpSessionMessages.SendFailed, failure);
