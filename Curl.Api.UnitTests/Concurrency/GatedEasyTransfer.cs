using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.Concurrency;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose perform waits until the test ends it with <see cref="End"/> or
/// <see cref="Throw"/>, or until its perform token is cancelled, which ends it with
/// <see cref="CurlCode.AbortedByCallback"/>. <see cref="Performing"/> completes once the perform started.
/// </summary>
internal sealed class GatedEasyTransfer : IEasyTransfer
{
    private readonly TaskCompletionSource<CurlCode> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _performing = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GatedEasyTransfer(string url = "http://example/")
    {
        Options = new EasyOptions { Network = new EasyOptions().Network with { Url = url } };
    }

    public EasyOptions Options { get; set; }

    public TransferInfo Info { get; } = new();

    public string ErrorText => "";

    public Task Performing => _performing.Task;

    public bool PerformTokenCancelled { get; private set; }

    public bool Disposed { get; private set; }

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public void End(CurlCode code) => _result.TrySetResult(code);

    public void Throw(Exception exception) => _result.TrySetException(exception);

    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        _performing.TrySetResult();
        using (cancellationToken.Register(() =>
        {
            PerformTokenCancelled = true;
            End(CurlCode.AbortedByCallback);
        }))
        {
            return await _result.Task;
        }
    }

    public IEasyTransfer Duplicate() => throw new NotSupportedException();

    public void Reset() => throw new NotSupportedException();

    public CurlCode Pause(CurlPause directions) => CurlCode.Ok;

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
