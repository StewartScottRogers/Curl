using System.Text;
using Curl.Api.Callbacks;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Hands every transfer event to a <c>CURLOPT_DEBUGFUNCTION</c>, as libcurl's <c>Curl_debug</c> does under
/// <c>CURLOPT_VERBOSE</c> (ADR-0543): an info line as <see cref="CurlInfoType.Text" /> with its line feed,
/// the structured events (a connection opened or reused, a TLS handshake, trust or message) as the
/// <see cref="CurlInfoType.Text" /> lines <c>-v</c> words them with, request and response head bytes as
/// <see cref="CurlInfoType.HeaderOut" /> and <see cref="CurlInfoType.HeaderIn" />, body bytes as
/// <see cref="CurlInfoType.DataOut" /> and <see cref="CurlInfoType.DataIn" />, and TLS record bytes as
/// <see cref="CurlInfoType.SslDataOut" /> and <see cref="CurlInfoType.SslDataIn" /> in the OpenSSL build
/// only, as the Schannel build passes none. The callback's return value is ignored, as libcurl ignores it.
/// </summary>
/// <param name="debug">The debug callback.</param>
/// <param name="tlsBackend">The curl build whose TLS wording the TLS events get (ADR-0085).</param>
internal sealed class DebugCallbackTransferEvents(DebugCallback debug, TlsBackend tlsBackend) : ITransferEvents
{
    /// <summary>The most bytes an info line, line feed included, reaches the callback with uncut: 2046.</summary>
    internal const int MaxInfoLineBytes = 2046;

    /// <summary>
    /// The events a handle's transfer reports to: the debug callback under <c>CURLOPT_VERBOSE</c>, worded as
    /// the running platform's curl build words TLS; <see langword="null" />, for no events, when verbose is
    /// off or no debug callback is set.
    /// </summary>
    /// <param name="verbose">Whether <c>CURLOPT_VERBOSE</c> is set.</param>
    /// <param name="debug">The <c>CURLOPT_DEBUGFUNCTION</c>, or <see langword="null" />.</param>
    /// <returns>The events, or <see langword="null" />.</returns>
    internal static DebugCallbackTransferEvents? Of(bool verbose, DebugCallback? debug) =>
        verbose && debug is not null ? new DebugCallbackTransferEvents(debug, PlatformTlsBackend.ForProcess) : null;

    /// <summary>
    /// The events a handle's transfer reports to under <c>CURLOPT_VERBOSE</c>: the debug callback when one is set,
    /// else libcurl's default one (<see cref="StandardErrorDebugWriter" />) writing to <paramref name="standardError" />,
    /// as <c>Curl_debug</c> falls back to <c>CURLOPT_STDERR</c> (BL-2726); <see langword="null" />, for no events, when
    /// verbose is off.
    /// </summary>
    /// <param name="verbose">Whether <c>CURLOPT_VERBOSE</c> is set.</param>
    /// <param name="debug">The <c>CURLOPT_DEBUGFUNCTION</c>, or <see langword="null" />.</param>
    /// <param name="standardError">Gives the stream the default writer writes to, asked for only when it is used.</param>
    /// <returns>The events, or <see langword="null" />.</returns>
    internal static DebugCallbackTransferEvents? Of(bool verbose, DebugCallback? debug, Func<Stream> standardError) =>
        verbose ? new DebugCallbackTransferEvents(debug ?? new StandardErrorDebugWriter(standardError()).Write, PlatformTlsBackend.ForProcess) : null;

    /// <inheritdoc />
    public void ReportInfo(string text) => ReportText(text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => ReportText(TransferEventInfoText.ConnectionOpened(opened));

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => ReportText(TransferEventInfoText.ConnectionReused(reused));

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => ReportTextLines(TransferEventInfoText.TlsHandshake(handshake, tlsBackend));

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
    {
        if (tlsBackend == TlsBackend.OpenSsl)
        {
            debug(sent ? CurlInfoType.SslDataOut : CurlInfoType.SslDataIn, bytes);
        }
    }

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message)
    {
        ReportTextLines(TransferEventInfoText.TlsMessage(message, tlsBackend));
        ReportTlsData(message.Bytes.Span, message.Sent);
    }

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => ReportTextLines(TransferEventInfoText.TlsTrust(trust, tlsBackend));

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => debug(CurlInfoType.HeaderOut, bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => debug(CurlInfoType.HeaderIn, bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => debug(CurlInfoType.DataOut, bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => debug(CurlInfoType.DataIn, bytes);

    private void ReportTextLines(IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
        {
            ReportText(line);
        }
    }

    /// <summary>
    /// Hands one info line to the callback with its line feed, cut as <c>Curl_infof</c>'s 2048-byte trace buffer
    /// cuts it (<c>TRC_LINE_MAX</c>, <c>lib/curl_trc.c</c>): a line whose bytes with the line feed come to more than
    /// <see cref="MaxInfoLineBytes" /> keeps its first 2043 bytes and ends in <c>...\n</c>, 2047 bytes in all.
    /// </summary>
    private void ReportText(string line)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
        debug(CurlInfoType.Text, bytes.Length <= MaxInfoLineBytes ? bytes : [.. bytes.AsSpan(0, MaxInfoLineBytes - 3), .. "...\n"u8]);
    }
}
