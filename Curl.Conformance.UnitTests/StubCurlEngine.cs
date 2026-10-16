using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="ICurlEngine"/> that only remembers the invocation it was made for, so a runner test can
/// see which engine a libtest program was handed; it makes no handle.
/// </summary>
/// <param name="invocation">The invocation the runner made the engine from.</param>
internal sealed class StubCurlEngine(UpstreamCurlInvocation invocation) : ICurlEngine
{
    public UpstreamCurlInvocation Invocation { get; } = invocation;

    public CurlVersionInfo VersionInfo { get; } = new();

    public IEasyTransfer CreateEasy() => throw new NotSupportedException();

    public IMultiTransfer CreateMulti() => throw new NotSupportedException();

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime() => throw new NotSupportedException();

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
