using System.Reflection;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;

namespace Libcurl.UnitTests.Global;

/// <summary>An <see cref="ICurlEngine"/> that counts its disposals and remembers the inert handles it hands out.</summary>
internal sealed class FakeCurlEngine : ICurlEngine
{
    public int DisposeCount { get; private set; }

    public List<object> Created { get; } = [];

    public CurlVersionInfo VersionInfo { get; } = new();

    public IEasyTransfer CreateEasy() => Remember(InertHandle.Create<IEasyTransfer>());

    public IMultiTransfer CreateMulti() => Remember(InertHandle.Create<IMultiTransfer>());

    public IShareHandle CreateShare() => Remember(InertHandle.Create<IShareHandle>());

    public ICurlUrlHandle CreateUrl() => Remember(InertHandle.Create<ICurlUrlHandle>());

    public IMimeBuilder CreateMime() => Remember(InertHandle.Create<IMimeBuilder>());

    public ICurlFormBuilder CreateForm() => Remember(InertHandle.Create<ICurlFormBuilder>());

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    private T Remember<T>(T handle)
        where T : class
    {
        Created.Add(handle);
        return handle;
    }
}

/// <summary>A handle of any seam interface whose every member does nothing and returns <see langword="null"/>.</summary>
public class InertHandle : DispatchProxy
{
    internal static T Create<T>()
        where T : class => Create<T, InertHandle>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
}
