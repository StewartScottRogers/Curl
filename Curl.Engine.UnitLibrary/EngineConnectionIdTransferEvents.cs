using System.Collections.Concurrent;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and keeps an easy handle's transfer's
/// <c>%{conn_id}</c> (<see cref="Api.Info.TransferInfo.ConnectionId" />) on <paramref name="state" /> right
/// for a reused connection, as <c>Curl.Console</c>'s <c>ConnectionIdRecordingTransferEvents</c> does for the
/// runner (BL-1052, BL-2362): a connection the transfer opens takes its <c>%{conn_id}</c> and is
/// remembered in <paramref name="connectionIds" /> under the pool's number for it, and a transfer that
/// reuses that connection takes the same <c>%{conn_id}</c>. A reused connection no transfer of the engine
/// saw open takes the pool's number for it (ADR-0109). Each <c>%{conn_id}</c> it takes goes to
/// <paramref name="connectionIdTaken" /> before the event goes on, so the handle's live
/// <see cref="Api.Info.TransferInfo.ConnectionId" /> is the connection's while the debug callback sees the
/// event (BL-2364, ADR-0547).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="state">The running transfer whose <see cref="EngineTransferState.ConnectionId" /> is kept.</param>
/// <param name="connectionIds">The engine's <c>%{conn_id}</c> of each connection, by the pool's number for it.</param>
/// <param name="takeConnectionId">Gives the transfer its <c>%{conn_id}</c>, taking the next one when it has none.</param>
/// <param name="connectionIdTaken">Told the transfer's <c>%{conn_id}</c> each time it opens or reuses a connection.</param>
/// <param name="connectionOpened">
/// Told each connection the transfer opens, other than a second (data) connection, before <paramref name="connectionIdTaken" />;
/// <see langword="null" /> for none.
/// </param>
internal sealed class EngineConnectionIdTransferEvents(
    ITransferEvents inner,
    EngineTransferState state,
    ConcurrentDictionary<long, long> connectionIds,
    Func<long> takeConnectionId,
    Action<long> connectionIdTaken,
    Action<ConnectionOpenedEvent>? connectionOpened = null) : ITransferEvents
{
    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        if (!opened.IsSecondConnection)
        {
            connectionOpened?.Invoke(opened);
            long connectionId = takeConnectionId();
            connectionIds[opened.ConnectionNumber] = connectionId;
            connectionIdTaken(connectionId);
        }

        inner.ReportConnectionOpened(opened);
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused)
    {
        long connectionId = connectionIds.GetValueOrDefault(reused.ConnectionNumber, reused.ConnectionNumber);
        state.ConnectionId = connectionId;
        connectionIdTaken(connectionId);
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
