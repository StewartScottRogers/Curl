using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;

namespace Curl.Api.Concurrency;

/// <summary>
/// An <see cref="ICurlEngine"/> that hands out a new <see cref="GatedEasyTransfer"/> for each easy handle
/// and one <see cref="EasyPerformingMultiTransfer"/>, counting how many multi handles were asked for.
/// </summary>
internal sealed class ConcurrentCurlEngine : ICurlEngine
{
    public EasyPerformingMultiTransfer Multi { get; } = new();

    public int MultiCount { get; private set; }

    public List<GatedEasyTransfer> CreatedEasyHandles { get; } = [];

    public CurlVersionInfo VersionInfo => throw new NotSupportedException();

    public IEasyTransfer CreateEasy()
    {
        GatedEasyTransfer easy = new();
        lock (CreatedEasyHandles)
        {
            CreatedEasyHandles.Add(easy);
        }

        return easy;
    }

    public IMultiTransfer CreateMulti()
    {
        MultiCount++;
        return Multi;
    }

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => throw new NotSupportedException();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
