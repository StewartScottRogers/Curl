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

namespace Curl.Api.Stores;

/// <summary>An <see cref="ICurlEngine"/> that hands out <see cref="StoreEasyTransfer"/>s and <see cref="StoreShareHandle"/>s.</summary>
internal sealed class StoreEngine : ICurlEngine
{
    public List<StoreEasyTransfer> CreatedEasyHandles { get; } = [];

    public List<StoreShareHandle> CreatedShares { get; } = [];

    /// <summary>What each new share handle answers to <see cref="IShareHandle.Share"/>, by kind.</summary>
    public Dictionary<CurlLockData, CurlShareCode> ShareAnswers { get; } = [];

    /// <summary>Runs inside <see cref="CreateShare"/>, before the handle is returned.</summary>
    public Action? OnCreateShare { get; set; }

    public int DisposeCount { get; private set; }

    public CurlVersionInfo VersionInfo => throw new NotSupportedException();

    /// <summary>Whether the handles it hands out yield before cleaning up.</summary>
    public bool HandlesYieldOnDispose { get; init; }

    public IEasyTransfer CreateEasy()
    {
        StoreEasyTransfer easy = new() { YieldsOnDispose = HandlesYieldOnDispose };
        CreatedEasyHandles.Add(easy);
        return easy;
    }

    public IShareHandle CreateShare()
    {
        StoreShareHandle share = new(ShareAnswers);
        CreatedShares.Add(share);
        Action? onCreateShare = OnCreateShare;
        OnCreateShare = null;
        onCreateShare?.Invoke();
        return share;
    }

    public IMultiTransfer CreateMulti() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => throw new NotSupportedException();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}

/// <summary>An <see cref="IShareHandle"/> that records what it shares and how often it is cleaned up.</summary>
internal sealed class StoreShareHandle(Dictionary<CurlLockData, CurlShareCode> answers) : IShareHandle
{
    public List<CurlLockData> Shared { get; } = [];

    public int CleanupCount { get; private set; }

    public CurlShareCode Share(CurlLockData data)
    {
        Shared.Add(data);
        return answers.GetValueOrDefault(data, CurlShareCode.Ok);
    }

    public CurlShareCode Unshare(CurlLockData data) => throw new NotSupportedException();

    public CurlShareCode SetLockCallback(ShareLockCallback? callback) => throw new NotSupportedException();

    public CurlShareCode SetUnlockCallback(ShareUnlockCallback? callback) => throw new NotSupportedException();

    public CurlShareCode Cleanup()
    {
        CleanupCount++;
        return CurlShareCode.Ok;
    }
}

/// <summary>
/// An <see cref="IEasyTransfer"/> whose perform records the options it ran with and, like libcurl,
/// preloads HSTS through the read callback; its cleanup hands <see cref="HstsCacheAtCleanup"/> to the
/// write callback. Every other member answers a fixed value and counts its calls.
/// </summary>
internal sealed class StoreEasyTransfer : IEasyTransfer
{
    public EasyOptions Options { get; set; } = new();

    public EasyOptions? PerformedOptions { get; private set; }

    public List<Callbacks.HstsEntry> PreloadedHsts { get; } = [];

    public List<Callbacks.HstsEntry> HstsCacheAtCleanup { get; } = [];

    public TransferInfo Info { get; set; } = new();

    public string ErrorText => "error text";

    public ITransferHeaders Headers => throw new InvalidOperationException("headers");

    public IEasyWebSocket WebSocket => throw new InvalidOperationException("websocket");

    public IConnectOnlyConnection Connection => throw new InvalidOperationException("connection");

    public int ResetCount { get; private set; }

    public CurlPause? PausedWith { get; private set; }

    public int UpkeepCount { get; private set; }

    public int DisposeCount { get; private set; }

    public StoreEasyTransfer? Duplicated { get; private set; }

    public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedOptions = Options;
        if (Options.Callbacks.HstsRead is { } read)
        {
            while (read(out Callbacks.HstsEntry? entry) == Callbacks.HstsResult.Ok)
            {
                PreloadedHsts.Add(entry!);
            }
        }

        return ValueTask.FromResult(CurlCode.Ok);
    }

    public IEasyTransfer Duplicate()
    {
        Duplicated = new StoreEasyTransfer { Options = Options };
        return Duplicated;
    }

    public void Reset() => ResetCount++;

    public CurlCode Pause(CurlPause directions)
    {
        PausedWith = directions;
        return CurlCode.Ok;
    }

    public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken)
    {
        UpkeepCount++;
        return ValueTask.FromResult(CurlCode.Again);
    }

    /// <summary>Whether <see cref="DisposeAsync"/> yields before cleaning up, as an engine cleaning up off the calling thread does.</summary>
    public bool YieldsOnDispose { get; init; }

    public async ValueTask DisposeAsync()
    {
        if (YieldsOnDispose)
        {
            await Task.Yield();
        }

        DisposeCount++;
        if (Options.Callbacks.HstsWrite is { } write)
        {
            for (int index = 0; index < HstsCacheAtCleanup.Count; index++)
            {
                write(HstsCacheAtCleanup[index], index, HstsCacheAtCleanup.Count);
            }
        }
    }
}
