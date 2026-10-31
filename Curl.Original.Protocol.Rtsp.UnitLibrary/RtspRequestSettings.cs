namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// The RTSP request settings of one libcurl perform: <c>CURLOPT_RTSP_REQUEST</c>,
/// <c>CURLOPT_RTSP_STREAM_URI</c>, <c>CURLOPT_RTSP_TRANSPORT</c>, <c>CURLOPT_RTSP_SESSION_ID</c>,
/// <c>CURLOPT_RTSP_CLIENT_CSEQ</c> and the upload or post body (BL-2751, ADR-0691).
/// </summary>
/// <remarks>
/// The curl tool gives none, and its transfer sends <c>OPTIONS *</c> as before. The engine maps
/// libcurl's options to this type, so the RTSP library needs no reference to the API library.
/// </remarks>
public sealed record RtspRequestSettings
{
    /// <summary>Gets the request to make; <see cref="RtspRequestKind.Options" /> by default.</summary>
    public RtspRequestKind Kind { get; init; } = RtspRequestKind.Options;

    /// <summary>Gets the request target, or <see langword="null" /> for <c>*</c>.</summary>
    public string? StreamUri { get; init; }

    /// <summary>Gets the <c>Transport</c> value a <c>SETUP</c> sends, or <see langword="null" /> for none.</summary>
    public string? Transport { get; init; }

    /// <summary>
    /// Gets the session ID to hold from this perform on, or <see langword="null" /> to keep the one
    /// the session state holds.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the <c>CSeq</c> the next request is sent with, or <see langword="null" /> to continue the
    /// session state's counter.
    /// </summary>
    public long? ClientSequenceNumber { get; init; }

    /// <summary>
    /// Gets the body an <c>ANNOUNCE</c>, <c>GET_PARAMETER</c> or <c>SET_PARAMETER</c> sends, or
    /// <see langword="null" /> for none; other requests send no body.
    /// </summary>
    public byte[]? Body { get; init; }
}
