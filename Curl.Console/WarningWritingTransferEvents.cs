using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and writes each reported warning to
/// <paramref name="standardError" /> as curl's tool does, <c>Warning: &lt;text&gt;</c> and a line
/// feed, wrapped as curl's <c>warnf</c> wraps it at <see cref="TerminalColumns.Default" /> columns, with or without <c>-v</c> (BL-3986). <see cref="TransferEventOutput" /> leaves it out
/// under <c>-s</c>, which silences curl's warnings.
/// </summary>
/// <param name="inner">The run's <c>-v</c> or trace sink.</param>
/// <param name="standardError">Standard error, in text mode.</param>
internal sealed class WarningWritingTransferEvents(ITransferEvents inner, Stream standardError) : ITransferEvents
{
    /// <summary>
    /// Gets the sink every event is passed on to.
    /// </summary>
    internal ITransferEvents Inner { get; } = inner;

    /// <inheritdoc />
    public void ReportWarning(string text)
    {
        foreach (string line in WarningLineWrapper.WrapText(text, TerminalColumns.Default))
        {
            standardError.Write(Encoding.UTF8.GetBytes(line + "\n"));
        }

        standardError.Flush();
    }

    /// <inheritdoc />
    public void ReportInfo(string text) => Inner.ReportInfo(text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Inner.ReportConnectionOpened(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => Inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Inner.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Inner.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => Inner.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => Inner.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => Inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => Inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Inner.ReportDataReceived(bytes);
}
