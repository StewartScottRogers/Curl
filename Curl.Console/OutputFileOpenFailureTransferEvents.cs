using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged, except the handler's
/// <c>Failure writing output to destination</c> info line once the <c>-o</c> file could not be opened:
/// curl 8.22.0 writes that line only for a write callback that took fewer bytes than it was given, and
/// for the tool's failed open it writes its <c>Warning: Failed to open the file</c> line and then
/// <c>* client returned ERROR on write of N bytes</c>, N being the size of the write the file refused
/// (measured 2026-10-28 with <c>-v -o nodir/x.txt file:///...</c>, BL-3951, CGR-088). The warning is
/// written to <paramref name="warningOutput" /> just before the info line, in curl's order, and is then
/// no longer held for the end of the transfer.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="state">The running transfer whose <c>-o</c> file is read.</param>
/// <param name="warningOutput">Where the warning goes, or <see langword="null" /> when <c>-s</c> hides it.</param>
internal sealed class OutputFileOpenFailureTransferEvents(ITransferEvents inner, RunningTransferState state, Stream? warningOutput) : ITransferEvents
{
    private const string HandlersWriteFailure = "Failure writing output to destination, passed ";

    /// <inheritdoc />
    public void ReportInfo(string text) => inner.ReportInfo(ClientWriteErrorFor(text) ?? text);

    /// <summary>
    /// Gives curl's line for <paramref name="text" /> when it is the handler's write failure and the file could not
    /// be opened, writing the pending warning first.
    /// </summary>
    /// <param name="text">The info line.</param>
    /// <returns>curl's <c>client returned ERROR on write of N bytes</c>, or <see langword="null" /> to keep the line.</returns>
    private string? ClientWriteErrorFor(string text)
    {
        if (!text.StartsWith(HandlersWriteFailure, StringComparison.Ordinal) || state.OutputFile?.FailedWriteLength is not { } length)
        {
            return null;
        }

        WriteWarning(state.OutputFile.TakeOpenFailureWarning());

        return "client returned ERROR on write of " + length.ToString(CultureInfo.InvariantCulture) + " bytes";
    }

    /// <summary>Writes the warning line to <c>warningOutput</c>, when there is both.</summary>
    /// <param name="warning">The warning, or <see langword="null" />.</param>
    private void WriteWarning(string? warning)
    {
        if (warning is null || warningOutput is null)
        {
            return;
        }

        warningOutput.Write(Encoding.UTF8.GetBytes(warning + Environment.NewLine));
        warningOutput.Flush();
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => inner.ReportConnectionOpened(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => inner.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => inner.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => inner.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => inner.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
