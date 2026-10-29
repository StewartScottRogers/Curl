using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

namespace Curl.Engine;

internal sealed partial class ManagedMultiTransfer
{
    // Marks the code a transfer's perform runs, its callbacks included, with the handle that started it
    // (IsInTransferCallback); the value flows into the perform and never back out to the caller.
    private async Task<CurlCode> Start(MultiEntry entry)
    {
        entry.StartTimestamp = timeProvider.GetTimestamp();
        entry.Origin = OriginOf(entry.Transfer);
        entry.Multiplexed = IsMultiplexed(entry.Transfer, entry.Origin);
        await CloseIdleConnectionsForRoomAsync(entry.Transfer).ConfigureAwait(false);
        PerformingHandle.Value = this;
        TransferCallbackMarshal callbacks = new(callbackQueue, entry.Cancellation.Token);
        if (entry.Transfer.PerformingTransfer is ManagedEasyTransfer easy)
        {
            easy.MultiCallbacks = callbacks;
            easy.SocketWait = entry.SocketRecord = SocketWaitFor(entry);
        }

        try
        {
            return await entry.Transfer.PerformAsync(entry.Cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            callbacks.Stop();
        }
    }

    /// <summary>
    /// Under <see cref="MultiOptions.MaxTotalConnections" />, closes idle connections so the one
    /// <paramref name="transfer" /> may open stays within it, as libcurl counts idle connections against the limit and
    /// closes its oldest idle one to make room (CGR-150).
    /// </summary>
    private ValueTask CloseIdleConnectionsForRoomAsync(IEasyTransfer transfer)
    {
        long limit = Options.MaxTotalConnections;
        if (limit <= 0)
        {
            return ValueTask.CompletedTask;
        }

        bool parsed = Uri.TryCreate(transfer.Options.Network.Url, UriKind.Absolute, out Uri? uri);
        return CloseIdleConnectionsToMakeRoom(limit, parsed ? uri!.IdnHost : string.Empty, parsed ? uri!.Port : 0);
    }

    /// <summary>
    /// The socket wait a transfer of this engine runs on: while the application has a socket callback, the
    /// entry's <see cref="MultiEntry.SocketWait" />, so its I/O waits for the application's poll (ADR-0821,
    /// ADR-0973); without one, a wait whose gating is stopped from the start, so perform and wait drive the transfer
    /// and the wait only records its socket for <see cref="GetWaitSockets" /> (ADR-0982).
    /// </summary>
    private TransferSocketWait SocketWaitFor(MultiEntry entry)
    {
        TransferSocketWait wait = new(timeProvider);
        if (Options.Socket is null)
        {
            wait.StopGating();
            entry.SocketWait = null;
            return wait;
        }

        wait.Changed += entry.MarkChanged;
        entry.SocketWait = wait;
        return wait;
    }

    private long RemainingMilliseconds(MultiEntry entry)
    {
        TimeSpan limit = entry.Transfer.Options.Transfer.Timeout;
        if (entry.Done || entry.Perform is null || limit <= TimeSpan.Zero)
        {
            return -1;
        }

        TimeSpan left = limit - timeProvider.GetElapsedTime(entry.StartTimestamp);
        return Math.Max(0, (long)Math.Ceiling(left.TotalMilliseconds));
    }

}
