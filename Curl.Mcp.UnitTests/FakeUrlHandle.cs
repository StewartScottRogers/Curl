using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Mcp;

/// <summary>A URL handle that answers each get from <see cref="Parts" /> and records each set.</summary>
internal sealed class FakeUrlHandle : ICurlUrlHandle
{
    /// <summary>What each get answers; a part not named answers <see cref="CurlUrlCode.UnknownPart" />.</summary>
    public Dictionary<CurlUrlPart, (CurlUrlCode Code, string? Value)> Parts { get; } = [];

    /// <summary>What a set of each part returns; a part not named returns <see cref="CurlUrlCode.Ok" />.</summary>
    public Dictionary<CurlUrlPart, CurlUrlCode> SetResults { get; } = [];

    /// <summary>Every set, in order.</summary>
    public List<(CurlUrlPart Part, string? Value, CurlUrlFlags Flags)> Sets { get; } = [];

    /// <summary>The flags of every get, in order.</summary>
    public List<CurlUrlFlags> GetFlags { get; } = [];

    /// <summary>Whether the handle was disposed.</summary>
    public bool IsDisposed { get; private set; }

    public CurlUrlCode Get(CurlUrlPart part, CurlUrlFlags flags, out string? value)
    {
        GetFlags.Add(flags);
        (CurlUrlCode code, value) = Parts.TryGetValue(part, out (CurlUrlCode, string?) answer) ? answer : (CurlUrlCode.UnknownPart, null);
        return code;
    }

    public CurlUrlCode Set(CurlUrlPart part, string? value, CurlUrlFlags flags)
    {
        Sets.Add((part, value, flags));
        return SetResults.GetValueOrDefault(part, CurlUrlCode.Ok);
    }

    public ICurlUrlHandle Duplicate() => throw new NotSupportedException();

    public void Dispose() => IsDisposed = true;
}
