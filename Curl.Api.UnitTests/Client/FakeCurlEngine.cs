using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;

namespace Curl.Api.Client;

/// <summary>An <see cref="ICurlEngine"/> that hands out <see cref="FakeEasyTransfer"/>s and counts its disposals.</summary>
internal sealed class FakeCurlEngine : ICurlEngine
{
    public int DisposeCount { get; private set; }

    public List<FakeEasyTransfer> CreatedEasyHandles { get; } = [];

    public CurlVersionInfo VersionInfo => throw new NotSupportedException();

    public IEasyTransfer CreateEasy()
    {
        FakeEasyTransfer easy = new();
        CreatedEasyHandles.Add(easy);
        return easy;
    }

    public IMultiTransfer CreateMulti() => throw new NotSupportedException();

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => throw new NotSupportedException();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
