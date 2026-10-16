using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Libcurl.UnitTests.Url;

/// <summary>An <see cref="ICurlUrlHandle"/> that records each call and answers what the test sets.</summary>
internal sealed class FakeCurlUrlHandle : ICurlUrlHandle
{
    public CurlUrlCode Result { get; init; } = CurlUrlCode.Ok;

    public string? GetValue { get; init; }

    public List<(CurlUrlPart Part, CurlUrlFlags Flags)> Gets { get; } = [];

    public List<(CurlUrlPart Part, string? Value, CurlUrlFlags Flags)> Sets { get; } = [];

    public FakeCurlUrlHandle? Duplicated { get; private set; }

    public int DisposeCount { get; private set; }

    public CurlUrlCode Get(CurlUrlPart part, CurlUrlFlags flags, out string? value)
    {
        Gets.Add((part, flags));
        value = GetValue;
        return Result;
    }

    public CurlUrlCode Set(CurlUrlPart part, string? value, CurlUrlFlags flags)
    {
        Sets.Add((part, value, flags));
        return Result;
    }

    public ICurlUrlHandle Duplicate() => Duplicated = new FakeCurlUrlHandle();

    public void Dispose() => DisposeCount++;
}
