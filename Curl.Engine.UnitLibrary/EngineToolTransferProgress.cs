using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The curl tool's runner's progress meter for one transfer and the token its <c>-Z</c> <c>--fail-early</c> aborts
/// through, which a <see cref="ManagedEasyTransfer" /> given them through <see cref="ManagedEasyTransfer.ToolProgress" />
/// reports to and stops on in place of its own progress callback, starting the attempt's <c>-Y</c>/<c>-y</c> and
/// <c>-m</c> watchdogs itself (BL-2398, ADR-0567). <see cref="Progress" /> is the runner's
/// <c>TransferProgressRecorder</c>, which takes every <see cref="ITransferProgress" /> report - the transfer's start,
/// its retries and resends as well as the counts - so the meter and the <c>-#</c> bar are the runner's own.
/// </summary>
/// <param name="Progress">Where the transfer's progress goes.</param>
/// <param name="AbortToken">The token that aborts the transfer under <c>-Z</c> <c>--fail-early</c>.</param>
internal sealed record EngineToolTransferProgress(ITransferProgress Progress, CancellationToken AbortToken);
