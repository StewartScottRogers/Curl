using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and keeps the transfer's
/// <c>%{conn_id}</c> on <paramref name="state" /> right for a reused connection: a connection the
/// transfer opens takes its <c>%{conn_id}</c> and is remembered in <paramref name="connectionIds" />
/// under the pool's number for it, and a transfer that reuses that connection takes the same
/// <c>%{conn_id}</c>, as curl 8.21.0 printed <c>[1 0][0 0]</c> for
/// <c>-w '[%{num_connects} %{conn_id}]'</c> and two URLs on one kept-alive connection
/// (measured, BL-754 Notes; BL-1052). A reused connection no transfer here saw open, such as one a
/// group without <c>-w</c> opened, takes the pool's number for it, which is curl's connection number,
/// the <c>N</c> of <c>Connection #N</c> (ADR-0109).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="state">The running transfer whose <see cref="RunningTransferState.ConnectionId" /> is kept.</param>
/// <param name="connectionIds">The run's <c>%{conn_id}</c> of each connection, by the pool's number for it.</param>
/// <param name="takeConnectionId">Gives the transfer its <c>%{conn_id}</c>, taking the next one when it has none.</param>
internal sealed class ConnectionIdRecordingTransferEvents(
    ITransferEvents inner,
    RunningTransferState state,
    ConcurrentDictionary<long, long> connectionIds,
    Func<long> takeConnectionId) : ITransferEvents
{
    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        if (!opened.IsSecondConnection)
        {
            connectionIds[opened.ConnectionNumber] = takeConnectionId();
        }

        inner.ReportConnectionOpened(opened);
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused)
    {
        state.ConnectionId = connectionIds.GetValueOrDefault(reused.ConnectionNumber, reused.ConnectionNumber);
        inner.ReportConnectionReused(reused);
    }

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportInfo(string text) => inner.ReportInfo(text);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => inner.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => inner.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => inner.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => inner.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
