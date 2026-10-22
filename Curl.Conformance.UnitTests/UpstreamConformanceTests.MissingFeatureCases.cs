using System.Text;
using Curl.Engine;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Upstream runs a case naming !proxy, !IPv6 or !IDN on a libcurl built without that feature; Curl is one build, so
    // the harness takes the feature off the platform for that case and runs curl with the engine's feature mask hiding
    // it, which answers as a build without it does (ADR-0746, BL-2856): 375, 1454 and 959 to 961.
    private static readonly (string Feature, EngineFeatureMask Mask)[] MaskableFeatures =
    [
        ("proxy", EngineFeatureMask.Proxy),
        ("IPv6", EngineFeatureMask.Ipv6),
        ("IDN", EngineFeatureMask.Idn),
    ];

    internal static string[] HiddenFeaturesFor(byte[] testFile)
    {
        string text = Encoding.Latin1.GetString(testFile);
        return [.. MaskableFeatures.Select(entry => entry.Feature).Where(feature => text.Contains("\n!" + feature + "\n", StringComparison.Ordinal))];
    }

    internal static EngineFeatureMask FeatureMaskFor(byte[] testFile) =>
        HiddenFeaturesFor(testFile).Aggregate(EngineFeatureMask.None, (mask, feature) => mask | MaskableFeatures.First(entry => entry.Feature == feature).Mask);
}
