using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// One attempt the curl tool's runner has already set up - its context, with the tool's output, header,
/// progress, event and watchdog wiring - for a <see cref="IToolAttemptTransfer" /> to perform in place of the
/// context its <see cref="Api.EngineContracts.IEasyTransfer.Options" /> would build (ADR-0552). The runner's
/// serial attempts take this path until the engine builds the tool's context from the easy options alone.
/// </summary>
/// <param name="State">The running transfer, holding the attempt's <c>-Y</c>/<c>-y</c> and <c>-m</c> watchdogs.</param>
/// <param name="Follower">Performs the attempt, following redirects under <c>-L</c>.</param>
/// <param name="Context">The attempt's context.</param>
/// <param name="RedirectPolicy">The <c>-L</c> policy.</param>
internal sealed record ToolAttempt(EngineTransferState State, RedirectFollower Follower, TransferContext Context, RedirectPolicy RedirectPolicy);
