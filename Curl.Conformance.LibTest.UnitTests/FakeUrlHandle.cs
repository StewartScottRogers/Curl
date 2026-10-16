using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>An <see cref="ICurlUrlHandle"/> that holds parts it is set and answers them back.</summary>
internal sealed class FakeUrlHandle : ICurlUrlHandle
{
    private readonly Dictionary<CurlUrlPart, string?> parts = [];

    public CurlUrlCode Get(CurlUrlPart part, CurlUrlFlags flags, out string? value) =>
        parts.TryGetValue(part, out value) ? CurlUrlCode.Ok : CurlUrlCode.NoHost;

    public CurlUrlCode Set(CurlUrlPart part, string? value, CurlUrlFlags flags)
    {
        parts[part] = value;
        return CurlUrlCode.Ok;
    }

    public ICurlUrlHandle Duplicate() => new FakeUrlHandle();

    public void Dispose()
    {
    }
}
