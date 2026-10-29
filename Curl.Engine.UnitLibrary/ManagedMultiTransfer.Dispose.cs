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
    /// <summary>
    /// <c>curl_multi_cleanup</c>: removes every transfer, cancelling the running ones, and waits for
    /// their performs to end, then closes the handle's idle connections, as libcurl 8.22.0's closes its
    /// connection pool (CGR-149); later calls answer <see cref="CurlMultiCode.BadHandle" />. Called from inside
    /// one of its own transfers (<see cref="IsInTransferCallback" />) it does nothing, as libcurl refuses
    /// that call, since waiting for the perform it runs in would never end.
    /// </summary>
    /// <returns>A task that completes once every perform has ended.</returns>
    public async ValueTask DisposeAsync()
    {
        if (disposed || IsInTransferCallback)
        {
            return;
        }

        disposed = true;
        foreach (MultiEntry entry in entries)
        {
            entry.SocketWait?.StopGating();
            entry.Cancellation.Cancel();
            owners.TryRemove(entry.Transfer, out _);
        }

        Task[] performs = [.. entries.Select(entry => entry.Perform).OfType<Task>()];
        entries.Clear();
        messages.Clear();
        await Task.WhenAll(performs).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await idleConnectionsClosing.ConfigureAwait(false);
        await closeIdleConnections().ConfigureAwait(false);
    }

}
