using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Curl.Conformance.LibTest.UnitTests;

internal sealed class FakeFormBuilder : ICurlFormBuilder
{
    public CurlFormAddCode AddResult { get; init; } = CurlFormAddCode.Ok;

    public int FailingCall { get; init; }

    public int? SerializedLength { get; init; }

    public List<IReadOnlyList<CurlFormArgument>> Calls { get; } = [];

    public CurlFormAddCode Add(IReadOnlyList<CurlFormArgument> arguments)
    {
        Calls.Add(arguments);
        return Calls.Count == FailingCall ? CurlFormAddCode.Memory : AddResult;
    }

    public int Serialize(FormGetCallback append)
    {
        _ = append(SerializedLength is { } length ? new byte[length] : "abc"u8);
        return 0;
    }

    public EasyMimeBody Build() => new();
}
