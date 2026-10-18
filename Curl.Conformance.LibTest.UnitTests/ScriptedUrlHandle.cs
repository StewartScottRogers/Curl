using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>An <see cref="ICurlUrlHandle"/> whose every answer a test scripts, recording each set.</summary>
internal sealed class ScriptedUrlHandle : ICurlUrlHandle
{
    public Func<CurlUrlPart, string?, CurlUrlCode> Setting { get; init; } = (_, _) => CurlUrlCode.Ok;

    public Func<CurlUrlPart, (CurlUrlCode Code, string? Value)> Getting { get; init; } = _ => (CurlUrlCode.Ok, null);

    public Func<ICurlUrlHandle> Duplicating { get; init; } = () => new ScriptedUrlHandle();

    public List<(CurlUrlPart Part, string? Value)> Sets { get; } = [];

    public CurlUrlCode Get(CurlUrlPart part, CurlUrlFlags flags, out string? value)
    {
        (CurlUrlCode code, value) = Getting(part);
        return code;
    }

    public CurlUrlCode Set(CurlUrlPart part, string? value, CurlUrlFlags flags)
    {
        Sets.Add((part, value));
        return Setting(part, value);
    }

    public ICurlUrlHandle Duplicate() => Duplicating();

    public void Dispose()
    {
    }
}
