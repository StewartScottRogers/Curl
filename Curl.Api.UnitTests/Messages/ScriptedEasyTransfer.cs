using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Api.Messages;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose perform replays a script: it hands <see cref="HeaderLines"/>
/// to the header callback and <see cref="BodyBytes"/> to the write callback, then returns
/// <see cref="Result"/>. It records the options and token it performed with and whether it was disposed.
/// </summary>
internal sealed class ScriptedEasyTransfer : IEasyTransfer
{
    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info { get; set; } = new();

    public string ErrorText { get; set; } = "";

    public List<string> HeaderLines { get; } = [];

    public byte[] BodyBytes { get; set; } = [];

    public CurlCode Result { get; set; } = CurlCode.Ok;

    public EasyOptions? PerformedOptions { get; private set; }

    public CancellationToken PerformedToken { get; private set; }

    public long BodyBytesTaken { get; private set; }

    public bool Disposed { get; private set; }

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedOptions = Options;
        PerformedToken = cancellationToken;
        foreach (string line in HeaderLines)
        {
            Options.Callbacks.Header!(Encoding.Latin1.GetBytes(line));
        }

        BodyBytesTaken = Options.Callbacks.Write!(BodyBytes);
        return ValueTask.FromResult(Result);
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
