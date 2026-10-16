namespace Curl.Engine;

/// <summary>
/// A header output that wants to know which head is a CONNECT reply's: <see cref="ConnectReplyHeadWritingEvents" />
/// calls <see cref="MarkConnectReplyHead" /> just before it writes one, so the header API can give its headers
/// <c>CURLH_CONNECT</c> (ADR-0603).
/// </summary>
internal interface IConnectReplyHeadMarking
{
    /// <summary>Says the next head written is a CONNECT reply's.</summary>
    void MarkConnectReplyHead();
}
