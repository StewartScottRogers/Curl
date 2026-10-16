using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.Progress;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose perform calls the transfer-info callback once for each of
/// <see cref="Calls"/>, recording each answer in <see cref="Answers"/>; the first non-zero answer ends
/// the transfer with <see cref="CurlCode.AbortedByCallback"/>, else it hands <see cref="BodyBytes"/>
/// to the write callback and returns <see cref="CurlCode.Ok"/>. It keeps the options it performed with.
/// </summary>
internal sealed class ProgressEasyTransfer : IEasyTransfer
{
    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info { get; set; } = new();

    public string ErrorText => "";

    public List<(long DownloadTotal, long DownloadNow, long UploadTotal, long UploadNow)> Calls { get; } = [];

    public List<int> Answers { get; } = [];

    public byte[] BodyBytes { get; set; } = [];

    public EasyOptions? PerformedOptions { get; private set; }

    public bool Disposed { get; private set; }

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedOptions = Options;
        foreach ((long downloadTotal, long downloadNow, long uploadTotal, long uploadNow) in Calls)
        {
            int answer = Options.Callbacks.TransferInfo!(downloadTotal, downloadNow, uploadTotal, uploadNow);
            Answers.Add(answer);
            if (answer != 0)
            {
                return ValueTask.FromResult(CurlCode.AbortedByCallback);
            }
        }

        Options.Callbacks.Write!(BodyBytes);
        return ValueTask.FromResult(CurlCode.Ok);
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
