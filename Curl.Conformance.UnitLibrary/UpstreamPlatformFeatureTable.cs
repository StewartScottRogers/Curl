namespace Curl.Conformance;

/// <summary>
/// The registration table of the features <see cref="UpstreamCurlPlatform"/> reports: one line per
/// feature file. A new feature goes in a new file of its own and one line here.
/// </summary>
internal static class UpstreamPlatformFeatureTable
{
    /// <summary>The features every platform reports.</summary>
    public static readonly string[] Common =
    [
        .. UpstreamServedProtocolFeatures.Names,
        .. UpstreamCurlVersionLineFeatures.Names,
        .. UpstreamToolFeatures.Names,
        .. UpstreamHarnessFeatures.Names,
        .. UpstreamHttp2Features.Names,
    ];

    /// <summary>The features only Windows reports.</summary>
    public static readonly string[] Windows = [.. UpstreamWindowsFeatures.Names];

    /// <summary>The features Linux and macOS report.</summary>
    public static readonly string[] Unix = [.. UpstreamUnixFeatures.Names];
}
