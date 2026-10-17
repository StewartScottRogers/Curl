using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;
using Curl.Api.Callbacks;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>An <see cref="ICurlFormBuilder"/> that records every <c>curl_formadd</c> call and answers each with <see cref="AddResult"/>.</summary>
internal sealed class FakeFormBuilder : ICurlFormBuilder
{
    public CurlFormAddCode AddResult { get; init; } = CurlFormAddCode.Ok;

    public List<IReadOnlyList<CurlFormArgument>> Calls { get; } = [];

    public CurlFormAddCode Add(IReadOnlyList<CurlFormArgument> arguments)
    {
        Calls.Add(arguments);
        return AddResult;
    }

    public int Serialize(FormGetCallback append) => 0;

    public EasyMimeBody Build() => new();
}
