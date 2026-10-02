using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITransferEvents" /> that records the information lines, open and reuse events,
/// TLS trust and TLS handshakes it is given and ignores the rest.
/// </summary>
public sealed class RecordingTransferEvents : ITransferEvents
{
    /// <summary>
    /// Gets every <see cref="ReportInfo" /> text as <c>* text</c>, every request head line as
    /// <c>&gt; line</c> and every response header line as <c>&lt; line</c>, in order, as <c>-v</c> shows them.
    /// </summary>
    public List<string> Transcript { get; } = [];

    /// <summary>Gets every <see cref="ReportInfo" /> text, in order.</summary>
    public List<string> Info { get; } = [];

    /// <summary>Gets every <see cref="ReportConnectionReused" /> event, in order.</summary>
    public List<ConnectionReusedEvent> Reused { get; } = [];

    /// <summary>Gets every <see cref="ReportTlsHandshake" /> event, in order.</summary>
    public List<TlsHandshakeEvent> Handshakes { get; } = [];

    /// <summary>
    /// Gets or sets what runs with each <see cref="ReportInfo" /> text as it is reported, so a
    /// test can see what else had happened at that moment; <see langword="null" /> runs nothing.
    /// </summary>
    public Action<string>? OnInfo { get; init; }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        Info.Add(text);
        Transcript.Add("* " + text);
        OnInfo?.Invoke(text);
    }

    /// <summary>Gets every <see cref="ReportConnectionOpened" /> event, in order.</summary>
    public List<ConnectionOpenedEvent> Opened { get; } = [];

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Opened.Add(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => Reused.Add(reused);

    /// <summary>
    /// Gets every <see cref="ReportTlsTrust" /> and <see cref="ReportTlsHandshake" /> event, in
    /// the order reported.
    /// </summary>
    public List<object> TlsEvents { get; } = [];

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
        Handshakes.Add(handshake);
        TlsEvents.Add(handshake);
    }

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => TlsEvents.Add(trust);

    /// <summary>Gets every <see cref="ReportCertificateVerifyResult" /> code with whether it was the proxy's, in order.</summary>
    public List<(long VerifyResult, bool IsProxy)> VerifyResults { get; } = [];

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => VerifyResults.Add((verifyResult, isProxy));

    /// <summary>Gets every <see cref="ReportTlsMessage" /> message, in the order reported.</summary>
    public List<TlsMessageEvent> TlsMessages { get; } = [];

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => TlsMessages.Add(message);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
    {
    }

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
        var head = Encoding.Latin1.GetString(bytes);
        Transcript.AddRange(head[..^2].Split("\r\n").Select(line => "> " + line));
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) =>
        Transcript.Add("< " + Encoding.Latin1.GetString(bytes).TrimEnd('\r', '\n'));

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
    }
}
