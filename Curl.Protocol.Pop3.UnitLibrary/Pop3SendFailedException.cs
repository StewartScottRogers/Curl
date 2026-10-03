using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Ends a POP3 transfer when a command cannot be written to the connection, which curl 8.21.0
/// ends at once with exit 55 (BL-1252, BL-1342): <c>Send failure: &lt;words&gt;</c> for any
/// socket error, the socket filter's <c>failf</c> worded by <see cref="CurlSocketErrorText"/>,
/// and <c>Failed sending data to the peer</c>, <c>CURLE_SEND_ERROR</c>'s own text, for a failure
/// with no socket error in it.
/// </summary>
/// <param name="failure">What the connection threw.</param>
internal sealed class Pop3SendFailedException(IOException failure)
    : Exception(CurlSocketErrorText.SendFailure(failure) ?? Pop3SessionMessages.SendFailed, failure);
