using System.Globalization;

namespace Curl.Output;

/// <summary>
/// curl's <c>--trace-ids</c> marker, <c>[&lt;xfer&gt;-&lt;conn&gt;] </c>, which
/// <see cref="VerboseTransferEventWriter"/> and <see cref="TraceTransferEventWriter"/> write after
/// any <c>--trace-time</c> stamp on each line that starts an event: <c>[0-0] </c>, or
/// <c>[0-x] </c> before the transfer has a connection (measured 2026-09-29, BL-648 Notes).
/// </summary>
/// <remarks>
/// It holds the IDs of the transfer whose event is being written. A writer shared by several
/// transfers reads it for every line; each transfer reports through a
/// <see cref="TraceIdsTransferEvents"/> that sets it first. It starts as transfer 0 with no
/// connection.
/// </remarks>
public sealed class TraceIdsPrefix
{
    /// <summary>Gets the <c>%{xfer_id}</c> of the transfer whose event is being written.</summary>
    internal long TransferId { get; private set; }

    /// <summary>
    /// Gets the <c>%{conn_id}</c> of that transfer's connection, or <see langword="null"/> before it has one.
    /// </summary>
    internal long? ConnectionId { get; private set; }

    /// <summary>Gets the marker, trailing space included, such as <c>[1-0] </c>.</summary>
    internal string Text => string.Create(
        CultureInfo.InvariantCulture,
        $"[{TransferId}-{(ConnectionId is { } connectionId ? connectionId.ToString(CultureInfo.InvariantCulture) : "x")}] ");

    /// <summary>Sets the IDs the next lines carry.</summary>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>.</param>
    /// <param name="connectionId">Its connection's <c>%{conn_id}</c>, or <see langword="null"/> before it has one.</param>
    internal void Set(long transferId, long? connectionId)
    {
        TransferId = transferId;
        ConnectionId = connectionId;
    }
}
