using System.Threading.Channels;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.ResponseBodies;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose perform runs step by step as the test feeds it: each
/// <see cref="Deliver"/> hands a chunk to the write callback, as libcurl does, holding and handing it
/// again after <see cref="Pause"/> unpauses when the callback answers <see cref="WriteCallbackResult.Pause"/>;
/// <see cref="Finish"/> ends the perform with a result. A cancelled perform token ends it with
/// <see cref="CurlCode.AbortedByCallback"/>, and a write callback that takes less ends it with
/// <see cref="CurlCode.WriteError"/>.
/// </summary>
internal sealed class SteppedEasyTransfer : IEasyTransfer
{
    private readonly Channel<object> _steps = Channel.CreateUnbounded<object>();
    private readonly SemaphoreSlim _resumed = new(0);
    private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info { get; set; } = new();

    public string ErrorText { get; set; } = "";

    public CancellationToken PerformedToken { get; private set; }

    public CurlCode? PerformResult { get; private set; }

    public int PauseCount { get; private set; }

    public int ResumeCount { get; private set; }

    public Exception? PerformThrows { get; set; }

    /// <summary>Completes the first time the write callback answers <see cref="WriteCallbackResult.Pause"/>.</summary>
    public Task Paused => _paused.Task;

    public bool Disposed { get; private set; }

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public void Deliver(params byte[] chunk) => _steps.Writer.TryWrite(chunk);

    public void Finish(CurlCode result) => _steps.Writer.TryWrite(result);

    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedToken = cancellationToken;
        if (PerformThrows is not null)
        {
            await Task.Yield();
            throw PerformThrows;
        }

        try
        {
            PerformResult = await RunStepsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            PerformResult = CurlCode.AbortedByCallback;
        }

        return PerformResult.Value;
    }

    private async Task<CurlCode> RunStepsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            object step = await _steps.Reader.ReadAsync(cancellationToken);
            if (step is CurlCode result)
            {
                return result;
            }

            byte[] chunk = (byte[])step;
            long taken;
            while ((taken = Options.Callbacks.Write!(chunk)) == WriteCallbackResult.Pause)
            {
                PauseCount++;
                _paused.TrySetResult();
                await _resumed.WaitAsync(cancellationToken);
            }

            if (taken != chunk.Length)
            {
                return CurlCode.WriteError;
            }
        }
    }

    public CurlCode Pause(CurlPause directions)
    {
        if (directions == CurlPause.Continue)
        {
            ResumeCount++;
            _resumed.Release();
        }

        return CurlCode.Ok;
    }

    public IEasyTransfer Duplicate() => throw new NotSupportedException();

    public void Reset() => throw new NotSupportedException();

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
