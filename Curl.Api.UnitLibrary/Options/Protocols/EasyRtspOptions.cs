namespace Curl.Api.Options.Protocols;

/// <summary>The RTSP options of one easy handle, each defaulting to libcurl 8.22.0's default.</summary>
public sealed record EasyRtspOptions
{
    /// <summary><c>CURLOPT_RTSP_REQUEST</c>: the RTSP request to send. Default <see cref="RtspRequest.Options"/>.</summary>
    public RtspRequest Request { get; init; } = RtspRequest.Options;

    /// <summary>
    /// <c>CURLOPT_RTSP_SESSION_ID</c>: the session ID to send. Default <see langword="null"/>,
    /// meaning the one the server gave in its <c>SETUP</c> reply.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// How many times <c>CURLOPT_RTSP_SESSION_ID</c> has been set on the handle. Default 0. libcurl writes the
    /// session ID into the handle's RTSP state at each set, so a set that repeats the value before it, a
    /// <see langword="null"/> after a <see langword="null"/> among them, still replaces the ID the server gave
    /// since; the count tells the engine a set happened (BL-2787).
    /// </summary>
    public long SessionIdSetCount { get; init; }

    /// <summary><c>CURLOPT_RTSP_STREAM_URI</c>: the stream URI the request names. Default <c>*</c>.</summary>
    public string StreamUri { get; init; } = "*";

    /// <summary><c>CURLOPT_RTSP_TRANSPORT</c>: the <c>Transport:</c> header for <c>SETUP</c>. Default <see langword="null"/>.</summary>
    public string? Transport { get; init; }

    /// <summary><c>CURLOPT_RTSP_CLIENT_CSEQ</c>: the <c>CSeq</c> of the next request. Default 0.</summary>
    public long ClientCSeq { get; init; }

    /// <summary><c>CURLOPT_RTSP_SERVER_CSEQ</c>: the <c>CSeq</c> expected of the next server-sent request. Default 0.</summary>
    public long ServerCSeq { get; init; }
}
