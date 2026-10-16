using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The curl tool's runner's events for one transfer and the running transfer they keep, which a
/// <see cref="ManagedEasyTransfer" /> given them through <see cref="ManagedEasyTransfer.ToolEvents" /> reports to
/// and runs on in place of its own (BL-2397, ADR-0561). <see cref="Events" /> is the runner's whole chain - its
/// <c>HoldableStream</c>-backed <c>-v</c> and <c>--trace</c> writer, <c>TraceIdsTransferEvents</c>,
/// <c>MultiStateTraceEvents</c>, its <c>--trace-config</c> decorators and its
/// <c>ConnectionIdRecordingTransferEvents</c> - so every line on standard error is the runner's own, and
/// <see cref="State" /> is the running transfer that recorder numbers <c>%{conn_id}</c> on, which the handle
/// reads in place of numbering it again.
/// </summary>
/// <param name="Events">Where the transfer's events go.</param>
/// <param name="State">The running transfer: its watchdogs, and the <c>%{conn_id}</c> <see cref="Events" /> keeps.</param>
internal sealed record EngineToolTransferEvents(ITransferEvents Events, EngineTransferState State);
