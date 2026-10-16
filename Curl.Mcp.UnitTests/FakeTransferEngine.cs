using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Mcp;

/// <summary>An engine whose only easy handle is the <see cref="FakeEasyTransfer" /> it was given.</summary>
internal sealed class FakeTransferEngine(FakeEasyTransfer transfer) : ICurlEngine
{
    public int EasyHandlesCreated { get; private set; }

    public CurlVersionInfo VersionInfo => throw new NotSupportedException();

    public IEasyTransfer CreateEasy()
    {
        EasyHandlesCreated++;
        return transfer;
    }

    public IMultiTransfer CreateMulti() => throw new NotSupportedException();

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => throw new NotSupportedException();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// An easy handle that records the options set and, on perform, fires the header and write callbacks
/// with the lines and chunks it was given, then answers <see cref="Code" />.
/// </summary>
internal sealed class FakeEasyTransfer : IEasyTransfer
{
    public CurlCode Code { get; init; } = CurlCode.Ok;

    public IReadOnlyList<string> HeaderLines { get; init; } = [];

    public IReadOnlyList<byte[]> BodyChunks { get; init; } = [];

    public TransferInfo InfoToReport { get; init; } = new();

    public string ErrorTextToReport { get; init; } = string.Empty;

    /// <summary>When set, perform waits for the token and throws as a cancelled transfer would.</summary>
    public bool WaitsForCancellation { get; init; }

    /// <summary>When set, perform throws this exception after the callbacks.</summary>
    public Exception? Throws { get; init; }

    /// <summary>When set, perform cancels this source after the callbacks and still answers <see cref="Code" />.</summary>
    public CancellationTokenSource? CancelsAfterPerform { get; init; }

    public int Performs { get; private set; }

    public bool Disposed { get; private set; }

    public CancellationToken TokenSeen { get; private set; }

    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info => InfoToReport;

    public string ErrorText => ErrorTextToReport;

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        Performs++;
        TokenSeen = cancellationToken;
        foreach (string line in HeaderLines)
        {
            Options.Callbacks.Header!(Encoding.UTF8.GetBytes(line));
        }

        foreach (byte[] chunk in BodyChunks)
        {
            Options.Callbacks.Write!(chunk);
        }

        if (WaitsForCancellation)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (Throws is not null)
        {
            throw Throws;
        }

        CancelsAfterPerform?.Cancel();
        return Code;
    }

    public IEasyTransfer Duplicate() => throw new NotSupportedException();

    public void Reset() => throw new NotSupportedException();

    public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
