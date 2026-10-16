using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.Options.Http;
using Libcurl.UnitTests.Global;

namespace Libcurl.UnitTests.Mime.Form;

/// <summary>An <see cref="ICurlFormBuilder"/> that records each call and answers <see cref="Result"/> and <see cref="SerializeResult"/>.</summary>
internal sealed class FakeFormBuilder : ICurlFormBuilder
{
    public CurlFormAddCode Result { get; set; }

    public int SerializeResult { get; set; }

    public List<IReadOnlyList<CurlFormArgument>> Adds { get; } = [];

    public List<FormGetCallback> Serializers { get; } = [];

    public CurlFormAddCode Add(IReadOnlyList<CurlFormArgument> arguments)
    {
        Adds.Add(arguments);
        return Result;
    }

    public int Serialize(FormGetCallback append)
    {
        Serializers.Add(append);
        return SerializeResult;
    }

    /// <summary>A body with one part per successful add, named by its first argument's value.</summary>
    public EasyMimeBody Build() => new() { Parts = [.. Adds.Select(a => new EasyMimePart { Name = a[0].Value as string })] };
}

/// <summary>An <see cref="ICurlEngine"/> whose forms are <see cref="FakeFormBuilder"/>s; every other handle is <see cref="FakeCurlEngine"/>'s.</summary>
internal sealed class FakeFormEngine : ICurlEngine
{
    private readonly FakeCurlEngine inert = new();

    public List<FakeFormBuilder> Forms { get; } = [];

    public CurlFormAddCode Result { get; set; }

    public CurlVersionInfo VersionInfo => inert.VersionInfo;

    public IEasyTransfer CreateEasy() => inert.CreateEasy();

    public IMultiTransfer CreateMulti() => inert.CreateMulti();

    public IShareHandle CreateShare() => inert.CreateShare();

    public ICurlUrlHandle CreateUrl() => inert.CreateUrl();

    public IMimeBuilder CreateMime() => inert.CreateMime();

    public ICurlFormBuilder CreateForm()
    {
        var form = new FakeFormBuilder { Result = Result };
        Forms.Add(form);
        return form;
    }

    public ValueTask DisposeAsync() => inert.DisposeAsync();
}
