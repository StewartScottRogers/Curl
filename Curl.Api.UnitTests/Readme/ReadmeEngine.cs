using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Mime;

namespace Curl.Api.Readme;

/// <summary>
/// An <see cref="ICurlEngine"/> that answers every <see cref="CreateEasy"/> with a new handle from the
/// factory it was given, so the README's samples can run several transfers, MIME bodies and WebSockets.
/// </summary>
internal sealed class ReadmeEngine(Func<IEasyTransfer> createEasy) : ICurlEngine
{
    public CurlVersionInfo VersionInfo => throw new NotSupportedException();

    public IEasyTransfer CreateEasy() => createEasy();

    public IMultiTransfer CreateMulti() => new Concurrency.EasyPerformingMultiTransfer();

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => new DisposableFakeMimeBuilder();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
