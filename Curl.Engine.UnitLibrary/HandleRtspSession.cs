using Curl.Api.Options;
using Curl.Api.Options.Protocols;
using Curl.Original.Protocol.Rtsp;

namespace Curl.Engine;

/// <summary>
/// The RTSP state one easy handle keeps from perform to perform (BL-2704): the <c>CSeq</c> counter and session ID
/// libcurl 8.22.0 holds in <c>data-&gt;state</c>, and the mapping of the handle's <c>CURLOPT_RTSP_*</c> options and
/// upload or post body to the RTSP handler's <see cref="RtspRequestSettings" />.
/// </summary>
/// <remarks>
/// libcurl's <c>CURLOPT_RTSP_CLIENT_CSEQ</c> and <c>CURLOPT_RTSP_SESSION_ID</c> write that state once, at
/// <c>curl_easy_setopt</c>; here the options are a record read at each perform, so a value is handed on only when it
/// differs from the one handed on before, and an unchanged option leaves the counter to run on. A session ID is also
/// handed on when <see cref="EasyRtspOptions.SessionIdSetCount" /> says it was set again, so a NULL set after a NULL
/// forgets the ID the server gave since (BL-2787).
/// </remarks>
internal sealed class HandleRtspSession
{
    private long appliedClientSequenceNumber;

    private string? appliedSessionId;

    private long appliedSessionIdSetCount;

    /// <summary>Gets the <c>CSeq</c> counter and session ID the handle's RTSP performs share.</summary>
    internal RtspSessionState State { get; } = new();

    /// <summary>
    /// Gives the RTSP handler's settings for one perform of <paramref name="options" />: the request
    /// (<c>CURLOPT_RTSP_REQUEST</c>, <see cref="RtspRequest.None" /> sending <c>OPTIONS</c>), the stream URI, the
    /// <c>Transport</c>, a changed session ID or client <c>CSeq</c>, and <c>CURLOPT_POSTFIELDS</c> as the body unless
    /// <c>CURLOPT_UPLOAD</c> is set, when the handler reads the upload instead.
    /// </summary>
    /// <param name="options">The handle's options.</param>
    /// <returns>The settings.</returns>
    internal RtspRequestSettings SettingsFor(EasyOptions options)
    {
        EasyRtspOptions rtsp = options.Protocols.Rtsp;
        return new RtspRequestSettings
        {
            Kind = rtsp.Request == RtspRequest.None ? RtspRequestKind.Options : (RtspRequestKind)(int)rtsp.Request,
            StreamUri = rtsp.StreamUri,
            Transport = rtsp.Transport,
            SessionId = TakeChangedSessionId(rtsp),
            ClientSequenceNumber = TakeChangedClientSequenceNumber(rtsp.ClientCSeq),
            Body = options.Transfer.Upload ? null : PostBodyOf(options),
        };
    }

    private string? TakeChangedSessionId(EasyRtspOptions rtsp)
    {
        string? sessionId = rtsp.SessionId;
        if (sessionId == appliedSessionId && rtsp.SessionIdSetCount == appliedSessionIdSetCount)
        {
            return null;
        }

        appliedSessionId = sessionId;
        appliedSessionIdSetCount = rtsp.SessionIdSetCount;
        if (sessionId is null)
        {
            State.ForgetSessionId();
        }

        return sessionId;
    }

    private long? TakeChangedClientSequenceNumber(long clientSequenceNumber)
    {
        if (clientSequenceNumber == appliedClientSequenceNumber)
        {
            return null;
        }

        appliedClientSequenceNumber = clientSequenceNumber;
        return clientSequenceNumber;
    }

    private static byte[]? PostBodyOf(EasyOptions options) =>
        options.Http.PostFields is { } fields
            ? fields[..(int)(options.Http.PostFieldSize < 0 ? fields.Length : Math.Min(options.Http.PostFieldSize, fields.Length))].ToArray()
            : null;
}
