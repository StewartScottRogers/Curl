namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// What one RTSP request sends, as curl 8.22.0's <c>lib/rtsp.c</c> decides it from the request
/// kind (BL-2751): the curl tool's <c>OPTIONS *</c>, or a libcurl request from
/// <see cref="RtspRequestSettings" />.
/// </summary>
/// <param name="Kind">The request kind.</param>
/// <param name="Target">The request target: the stream URI, or <c>*</c>.</param>
/// <param name="Transport">The <c>Transport</c> value a <c>SETUP</c> sends, or <see langword="null" />.</param>
/// <param name="Body">The body, or <see langword="null" /> for none.</param>
/// <param name="IsToolTransfer">
/// Whether this is the curl tool's transfer, which sends curl's default <c>User-Agent</c> and a
/// <c>Range</c> on its <c>OPTIONS</c> as measured (BL-591, BL-1293).
/// </param>
internal sealed record RtspRequestPlan(RtspRequestKind Kind, string Target, string? Transport, byte[]? Body, bool IsToolTransfer)
{
    /// <summary>The exit 43 message for a <c>SETUP</c> with no <c>Transport</c>, <c>lib/rtsp.c</c>'s text.</summary>
    internal const string SetupWithoutTransport = "Refusing to issue an RTSP SETUP without a Transport: header.";

    private const string DefaultTarget = "*";

    private static readonly string[] MethodNames =
        ["OPTIONS", "DESCRIBE", "ANNOUNCE", "SETUP", "PLAY", "PAUSE", "TEARDOWN", "GET_PARAMETER", "SET_PARAMETER", "RECORD", "RECEIVE"];

    /// <summary>Gets the curl tool's request: <c>OPTIONS *</c>.</summary>
    internal static RtspRequestPlan ToolTransfer { get; } = new(RtspRequestKind.Options, DefaultTarget, null, null, true);

    /// <summary>Gets the method name written on the request line.</summary>
    internal string MethodName => MethodNames[(int)Kind - 1];

    /// <summary>
    /// Gets a value indicating whether a <c>-r</c> or <c>-C</c> range is sent: always for the tool,
    /// otherwise only on <c>PLAY</c>, <c>PAUSE</c> and <c>RECORD</c>, as <c>rtsp_setup_request</c> does.
    /// </summary>
    internal bool SendsRange => IsToolTransfer || Kind is RtspRequestKind.Play or RtspRequestKind.Pause or RtspRequestKind.Record;

    /// <summary>
    /// Gets the <c>Content-Type</c> a body is sent with: <c>application/sdp</c> for <c>ANNOUNCE</c>,
    /// <c>text/parameters</c> for <c>GET_PARAMETER</c> and <c>SET_PARAMETER</c>, or
    /// <see langword="null" /> for the kinds that send no body.
    /// </summary>
    internal string? BodyContentType => Kind switch
    {
        RtspRequestKind.Announce => "application/sdp",
        RtspRequestKind.GetParameter or RtspRequestKind.SetParameter => "text/parameters",
        _ => null,
    };

    /// <summary>Gets the body bytes sent after the head: none for a kind that sends no body.</summary>
    internal byte[] SentBody => BodyContentType is not null && Body is { } body ? body : [];

    /// <summary>
    /// Gets a value indicating whether the reply body is left unread, as for an empty
    /// <c>GET_PARAMETER</c> (a heartbeat), which <c>lib/rtsp.c</c> makes like a <c>HEAD</c>.
    /// </summary>
    internal bool SkipsReplyBody => Kind == RtspRequestKind.GetParameter && SentBody.Length == 0;

    /// <summary>
    /// Gets a value indicating whether the reply body is written to the body output: only for a
    /// libcurl <c>DESCRIBE</c> or <c>GET_PARAMETER</c>, the kinds <c>lib/rtsp.c</c>'s <c>rtsp_do</c>
    /// clears <c>no_body</c> for; every other kind, and the curl tool's <c>OPTIONS *</c>, reads and
    /// discards it (BL-2789, ADR-0169).
    /// </summary>
    internal bool WritesReplyBody => !IsToolTransfer && Kind is RtspRequestKind.Describe or RtspRequestKind.GetParameter;

    /// <summary>Gives the plan for <paramref name="settings" />, or the tool's when none are given.</summary>
    /// <param name="settings">The libcurl request settings, or <see langword="null" />.</param>
    /// <returns>The plan.</returns>
    internal static RtspRequestPlan For(RtspRequestSettings? settings) =>
        settings is null
            ? ToolTransfer
            : new(settings.Kind, settings.StreamUri ?? DefaultTarget, settings.Transport, settings.Body, false);

    /// <summary>
    /// Gives the plan with <paramref name="upload" />'s bytes as its body when it is a libcurl request that sends a
    /// body and the settings gave none, as curl 8.22.0's <c>rtsp_setup_body</c> reads <c>CURLOPT_UPLOAD</c>'s read
    /// callback for <c>ANNOUNCE</c>, <c>GET_PARAMETER</c> and <c>SET_PARAMETER</c> (BL-2704); else the plan itself.
    /// </summary>
    /// <param name="upload">The transfer's upload, or <see langword="null" /> for none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The plan to send.</returns>
    internal async ValueTask<RtspRequestPlan> WithUploadAsync(Stream? upload, CancellationToken cancellationToken)
    {
        if (IsToolTransfer || Body is not null || BodyContentType is null || upload is null)
        {
            return this;
        }

        using MemoryStream body = new();
        await upload.CopyToAsync(body, cancellationToken).ConfigureAwait(false);
        return this with { Body = body.ToArray() };
    }

    /// <summary>
    /// Gives the <c>Transport</c> value written by curl, or <see langword="null" />: only a
    /// <c>SETUP</c> writes one, and not when a custom header names <c>Transport</c>.
    /// </summary>
    /// <param name="customNamesTransport">Whether a custom header names <c>Transport</c>.</param>
    /// <returns>The value, or <see langword="null" />.</returns>
    internal string? TransportValue(bool customNamesTransport) =>
        Kind == RtspRequestKind.Setup && !customNamesTransport ? Transport : null;

    /// <summary>
    /// Determines whether <c>lib/rtsp.c</c> refuses the request before sending it: a <c>SETUP</c>
    /// with neither a <c>Transport</c> setting nor a custom header naming one.
    /// </summary>
    /// <param name="customNamesTransport">Whether a custom header names <c>Transport</c>.</param>
    /// <returns><see langword="true" /> when refused with exit 43 and <see cref="SetupWithoutTransport" />.</returns>
    internal bool LacksTransport(bool customNamesTransport) =>
        Kind == RtspRequestKind.Setup && Transport is null && !customNamesTransport;
}
