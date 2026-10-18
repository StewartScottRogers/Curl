using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;

namespace Curl.Api.Cancellation;

/// <summary>An <see cref="ICurlEngine"/> whose easy handle is <see cref="Transfer"/>, counting how often one is created.</summary>
internal sealed class CancellableCurlEngine : ICurlEngine
{
    public CancellableEasyTransfer Transfer { get; } = new();

    public int CreatedCount { get; private set; }

    public CurlVersionInfo VersionInfo => throw new NotSupportedException();

    public IEasyTransfer CreateEasy()
    {
        CreatedCount++;
        return Transfer;
    }

    public IMultiTransfer CreateMulti() => new Concurrency.EasyPerformingMultiTransfer();

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => throw new NotSupportedException();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
