using Curl.Api.Options.Protocols;

namespace Curl.Api;

/// <summary>
/// RTSP settings, mapped one to one onto <see cref="EasyRtspOptions"/> (BL-2503, ADR-0640). Each member
/// starts at libcurl 8.22.0's default. Set on <see cref="CurlClientOptions.Rtsp"/> for every request or
/// on <see cref="CurlRequest.Rtsp"/> for one; a request's record replaces the client's whole. An invalid
/// value throws as it is set.
/// </summary>
public sealed record CurlRtspOptions : IEasyOptionRecord<EasyRtspOptions>
{
    /// <summary><c>CURLOPT_RTSP_REQUEST</c>: the RTSP request sent. Default <see cref="RtspRequest.Options"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not an <see cref="RtspRequest"/> value.</exception>
    public RtspRequest Request
    {
        get;
        init => field = TransportOptionGuard.Defined(value, nameof(Request));
    } = RtspRequest.Options;

    /// <summary><c>CURLOPT_RTSP_SESSION_ID</c>: the session ID. Default <see langword="null"/>: the server's.</summary>
    public string? SessionId { get; init; }

    /// <summary><c>CURLOPT_RTSP_STREAM_URI</c>: the stream URI. Default <c>*</c>.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string StreamUri
    {
        get;
        init => field = TransportOptionGuard.NotNull(value, nameof(StreamUri));
    } = "*";

    /// <summary><c>CURLOPT_RTSP_TRANSPORT</c>: the <c>Transport</c> header for <c>SETUP</c>. Default <see langword="null"/>.</summary>
    public string? Transport { get; init; }

    /// <summary><c>CURLOPT_RTSP_CLIENT_CSEQ</c>: the next client <c>CSeq</c>. Default 0.</summary>
    public long ClientCSeq { get; init; }

    /// <summary><c>CURLOPT_RTSP_SERVER_CSEQ</c>: the next server <c>CSeq</c> expected. Default 0.</summary>
    public long ServerCSeq { get; init; }

    /// <summary>An <see cref="EasyRtspOptions"/> holding every member of this record.</summary>
    /// <returns>The seam's RTSP options.</returns>
    EasyRtspOptions IEasyOptionRecord<EasyRtspOptions>.ToEasy() => new()
    {
        Request = Request,
        SessionId = SessionId,
        StreamUri = StreamUri,
        Transport = Transport,
        ClientCSeq = ClientCSeq,
        ServerCSeq = ServerCSeq,
    };
}
