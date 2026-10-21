namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // No platform reports http/3, since every reference build refuses --http3 (ADR-0540), but the harness stands in
    // for upstream's nghttpx-h3 server, and RunCurlAsync parses as a QUIC build. So a case naming the http/3 server
    // (2500 to 2503) gets http/3 from the harness, and Debug, which 2501 names only because upstream runs its http/3
    // cases on debug builds; it uses nothing debug-only (ADR-0720, BL-2838). No other case gets either.
    private static readonly string[] Http3StandInFeatures = ["http/3", "Debug"];

    // ADR-0498 runs the http/3 cases off Windows only: there Curl matches the Schannel build, whose revocation check under
    // --cacert fails on the generated test CA (exit 60), so on Windows the ratchet
    // runs them but does not hold them, so a listed one is Inconclusive there and judged on Linux and macOS (ADR-0720).
    private static bool IsHeldOnThisPlatform(byte[] testFile) =>
        !(OperatingSystem.IsWindows() && NamesHttp3Server(System.Text.Encoding.Latin1.GetString(testFile)));

    /// <summary>
    /// <paramref name="platform"/> with <c>http/3</c> and <c>Debug</c> when the test file's <c>&lt;server&gt;</c> part
    /// names the <c>http/3</c> server the harness stands in for, else <paramref name="platform"/> itself.
    /// </summary>
    internal static UpstreamCurlPlatform WithHttp3StandIn(UpstreamCurlPlatform platform, string testFileText) =>
        NamesHttp3Server(testFileText) ? platform.WithFeatures(Http3StandInFeatures) : platform;

    private static bool NamesHttp3Server(string testFileText)
    {
        int start = testFileText.IndexOf("\n<server>\n", StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += "\n<server>\n".Length;
        int end = testFileText.IndexOf("</server>", start, StringComparison.Ordinal);
        return testFileText[start..(end < 0 ? testFileText.Length : end)].Split('\n').Contains("http/3");
    }
}
