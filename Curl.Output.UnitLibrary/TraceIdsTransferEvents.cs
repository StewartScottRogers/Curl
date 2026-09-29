using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// One transfer's view of a <c>-v</c> or trace writer under <c>--trace-ids</c>: every event it
/// reports is written with that transfer's <c>[&lt;xfer&gt;-&lt;conn&gt;] </c> marker
/// (<see cref="TraceIdsPrefix"/>).
/// </summary>
/// <param name="writer">The writer shared by every transfer of the run, built with <paramref name="prefix"/>.</param>
/// <param name="prefix">The marker <paramref name="writer"/> reads.</param>
/// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
/// <param name="connectionId">
/// Read at each event: the transfer's <c>%{conn_id}</c>, or <see langword="null"/> while it has no connection.
/// </param>
/// <remarks>
/// Each event sets the marker and is written while holding a lock on <paramref name="prefix"/>, so
/// transfers running at once under <c>-Z</c> never write each other's IDs.
/// </remarks>
public sealed class TraceIdsTransferEvents(
    ITransferEvents writer,
    TraceIdsPrefix prefix,
    long transferId,
    Func<long?> connectionId) : ITransferEvents
{
    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportInfo(text);
        }
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportConnectionOpened(opened);
        }
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportConnectionReused(reused);
        }
    }

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportTlsHandshake(handshake);
        }
    }

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportTlsData(bytes, sent);
        }
    }

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportTlsMessage(message);
        }
    }

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportTlsTrust(trust);
        }
    }

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportRequestHeader(bytes);
        }
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportResponseHeader(bytes);
        }
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportDataSent(bytes);
        }
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
        lock (prefix)
        {
            prefix.Set(transferId, connectionId());
            writer.ReportDataReceived(bytes);
        }
    }
}
