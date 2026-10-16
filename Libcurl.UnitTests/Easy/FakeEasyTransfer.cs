using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Libcurl.UnitTests.Easy;

/// <summary>
/// An <see cref="IEasyTransfer"/> whose perform answers <see cref="Result"/> and records the options and
/// token it ran with, and which counts its disposals. <see cref="Reset"/> and <see cref="Duplicate"/> follow
/// <see cref="IEasyTransfer"/>'s contract: a reset restores the default options and keeps the open connection,
/// a duplicate carries the options and nothing else. <see cref="Pause"/> and <see cref="UpkeepAsync"/> record
/// what they were given and answer <see cref="PauseAndUpkeepResult"/>. Its other members are not called by these tests.
/// </summary>
internal sealed class FakeEasyTransfer : IEasyTransfer
{
    public CurlCode Result { get; init; } = CurlCode.Ok;

    public EasyOptions? PerformedWith { get; private set; }

    public CancellationToken PerformedToken { get; private set; }

    public int DisposeCount { get; private set; }

    public EasyOptions Options { get; set; } = new();

    public TransferInfo Info { get; init; } = new();

    public string ErrorText => throw new NotSupportedException();

    public ITransferHeaders Headers => throw new NotSupportedException();

    public IEasyWebSocket WebSocket => throw new NotSupportedException();

    public IConnectOnlyConnection Connection => throw new NotSupportedException();

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedWith = Options;
        PerformedToken = cancellationToken;
        return ValueTask.FromResult(Result);
    }

    /// <summary>Stands for the connection a perform leaves open: a duplicate never carries it, a reset keeps it.</summary>
    public string? OpenConnection { get; set; }

    public int ResetCount { get; private set; }

    public IEasyTransfer Duplicate() => new FakeEasyTransfer { Result = Result, Options = Options };

    public void Reset()
    {
        ResetCount++;
        Options = new();
    }

    /// <summary>What <see cref="Pause"/> and <see cref="UpkeepAsync"/> answer.</summary>
    public CurlCode PauseAndUpkeepResult { get; init; } = CurlCode.Ok;

    /// <summary>The directions each <see cref="Pause"/> call was given, in order.</summary>
    public List<CurlPause> PausedWith { get; } = [];

    /// <summary>The token each <see cref="UpkeepAsync"/> call was given, in order.</summary>
    public List<CancellationToken> UpkeptWith { get; } = [];

    public CurlCode Pause(CurlPause directions)
    {
        PausedWith.Add(directions);
        return PauseAndUpkeepResult;
    }

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken)
    {
        UpkeptWith.Add(cancellationToken);
        return ValueTask.FromResult(PauseAndUpkeepResult);
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
