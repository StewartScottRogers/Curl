using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Messages;

namespace Curl.Api.Mime;

/// <summary>
/// An <see cref="ICurlEngine"/> whose easy handle is the <see cref="ScriptedEasyTransfer"/> it was given
/// and whose every <see cref="CreateMime"/> is a new fake builder, disposable unless <see cref="Disposable"/> is false.
/// </summary>
internal sealed class MimeCurlEngine(ScriptedEasyTransfer easy) : ICurlEngine
{
    public bool Disposable { get; set; } = true;

    public Action<FakeMimePartBuilder> ConfigurePart { get; set; } = _ => { };

    /// <summary>Every builder created, in order.</summary>
    public List<FakeMimeBuilder> Builders { get; } = [];

    public CurlVersionInfo VersionInfo => throw new NotSupportedException();

    public IEasyTransfer CreateEasy() => easy;

    public IMultiTransfer CreateMulti() => throw new NotSupportedException();

    public IShareHandle CreateShare() => throw new NotSupportedException();

    public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

    public IMimeBuilder CreateMime()
    {
        FakeMimeBuilder builder = Disposable ? new DisposableFakeMimeBuilder() : new FakeMimeBuilder();
        builder.ConfigurePart = ConfigurePart;
        Builders.Add(builder);
        return builder;
    }

    public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
