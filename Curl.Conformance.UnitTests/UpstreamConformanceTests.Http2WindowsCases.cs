namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Windows reports http/2 since BL-3268, which runs the http/2 cases there. These listed cases pass on Linux and macOS but
    // Windows's answer differs, so on Windows the ratchet runs them but does not hold them until the task named beside each fixes it.
    private static readonly IReadOnlySet<int> HttpTwoCasesNotYetHeldOnWindows = new HashSet<int>
    {
        1704, // curl exits 0 where upstream expects 8 after the HTTP/2 Upgrade 101 response
    };

    private static bool IsHeldOnThisPlatform(int testNumber) =>
        !(OperatingSystem.IsWindows() && HttpTwoCasesNotYetHeldOnWindows.Contains(testNumber));
}
