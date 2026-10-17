using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.Cancellation;

/// <summary>
/// An <see cref="IEasyTransfer"/> that behaves as libcurl does towards cancellation: its perform
/// ignores the token it is given and stops only when the transfer-info callback answers non-zero.
/// It runs to <see cref="Stage"/>, completes <see cref="Reached"/>, then asks the callback every
/// millisecond until it aborts (<see cref="CurlCode.AbortedByCallback"/>). In
/// <see cref="TransferStage.Paused"/> it asks nothing until <see cref="Pause"/> unpauses it, as a
/// paused libcurl transfer stays put.
/// </summary>
internal sealed class CancellableEasyTransfer : IEasyTransfer
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _resumed = new(0);

    public TransferStage Stage { get; set; }

    /// <summary>When set, the perform ends with it at once, before any stage.</summary>
    public CurlCode? ImmediateResult { get; set; }

    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info => new();

    public string ErrorText => "";

    public Task Reached => _reached.Task;

    public int ProgressCalls { get; private set; }

    public int ResumeCalls { get; private set; }

    public bool Disposed { get; private set; }

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        if (ImmediateResult is CurlCode result)
        {
            return result;
        }

        await RunToStageAsync();
        _reached.TrySetResult();
        while (AskProgress() == 0)
        {
            await Task.Delay(1, CancellationToken.None);
        }

        return CurlCode.AbortedByCallback;
    }

    private async Task RunToStageAsync()
    {
        switch (Stage)
        {
            case TransferStage.Uploading:
                Options.Callbacks.Read!(new byte[4]);
                break;
            case TransferStage.Downloading:
                Options.Callbacks.Write!([1]);
                break;
            case TransferStage.Paused:
                Options.Callbacks.Write!([1]);
                while (Options.Callbacks.Write!([2]) == WriteCallbackResult.Pause)
                {
                    _reached.TrySetResult();
                    await _resumed.WaitAsync(CancellationToken.None);
                }

                break;
        }
    }

    private int AskProgress()
    {
        ProgressCalls++;
        Assert.IsFalse(Options.Behaviour.NoProgress, "CURLOPT_NOPROGRESS must be off for the transfer-info callback to be called.");
        return Options.Callbacks.TransferInfo!(0, 0, 0, 0);
    }

    public CurlCode Pause(CurlPause directions)
    {
        if (directions == CurlPause.Continue)
        {
            ResumeCalls++;
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
