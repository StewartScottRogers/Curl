using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;

namespace Curl.Api.Messages;

/// <summary>An <see cref="ICurlEngine"/> whose one easy handle is the fake it was given.</summary>
internal sealed class ScriptedCurlEngine(IEasyTransfer easy) : ICurlEngine
{
    public CurlVersionInfo VersionInfo => throw new NotSupportedException();

    public IEasyTransfer CreateEasy() => easy;

    public IMultiTransfer CreateMulti() => new Concurrency.EasyPerformingMultiTransfer();

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => throw new NotSupportedException();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
