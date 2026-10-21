namespace Curl.Conformance;

/// <summary>The features only Windows reports: <c>win32</c> and the Schannel build.</summary>
internal static class UpstreamWindowsFeatures
{
    public static readonly string[] Names =
    [
        "win32",
        "Schannel",
    ];
}
