using Curl.Api.Callbacks;

namespace Curl.Api;

public sealed partial record CurlRequest
{
    /// <summary>
    /// <c>CURLOPT_DEBUGFUNCTION</c> with <c>CURLOPT_VERBOSE</c> set (BL-5797, ADR-1341): takes the same
    /// trace <c>curl -v</c> writes to standard error, one call per piece, typed by <see cref="CurlInfoType"/>
    /// (<c>Text</c>, <c>HeaderIn</c>, <c>HeaderOut</c>, <c>DataIn</c>, <c>DataOut</c>). A caller of
    /// <c>MailRecipientAllowFails</c> reads each server reply to a <c>RCPT TO</c> from the <c>HeaderIn</c> pieces
    /// (<c>550 ...</c> for a refused recipient). The pieces are the bytes <c>curl -v</c> would print, so nothing
    /// <c>-v</c> hides reaches the callback. Runs on the thread the engine calls from. Default
    /// <see langword="null"/>: no trace is taken and the transfer is not verbose.
    /// </summary>
    public DebugCallback? Debug { get; init; }
}
