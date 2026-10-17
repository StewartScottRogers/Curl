using Curl.Api.EngineContracts.Multi;

namespace Curl.Api;

public sealed partial class CurlClient
{
    /// <summary>Cancelled by <see cref="DisposeAsync"/>: aborts every transfer still running, its body included.</summary>
    private readonly CancellationTokenSource _closing = new();

    private MultiTransferDriver? _transfers;

    /// <summary>The driver every <c>SendAsync</c> transfer runs on, created with the client's first transfer.</summary>
    private MultiTransferDriver Transfers =>
        LazyInitializer.EnsureInitialized(ref _transfers, () => new MultiTransferDriver(CreateMulti));

    /// <summary><c>curl_multi_init</c> on the client's engine, with <see cref="CurlClientOptions.MaxConnections"/> and <see cref="CurlClientOptions.MaxConnectionsPerHost"/> set.</summary>
    /// <returns>The new multi handle; the driver disposes it.</returns>
    private IMultiTransfer CreateMulti()
    {
        IMultiTransfer multi = _engine.CreateMulti();
        multi.Options = multi.Options with
        {
            MaxTotalConnections = Options.MaxConnections,
            MaxHostConnections = Options.MaxConnectionsPerHost,
        };
        return multi;
    }

    /// <summary>Aborts every running transfer, waits for the driver to stop them and disposes the multi handle.</summary>
    /// <returns>A task that completes once the multi handle (if any) is disposed.</returns>
    private async Task StopTransfersAsync()
    {
        await _closing.CancelAsync().ConfigureAwait(false);
        if (_transfers is not null)
        {
            await _transfers.DisposeAsync().ConfigureAwait(false);
        }
    }
}
