using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Plugin;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>
/// curl's <c>-s</c> and <c>-S</c> checked for one extended scheme against curl's manual page for curl 8.22.0
/// (curl.se/docs/manpage.html, "-s, --silent": "Silent or quiet mode. Do not show progress meter or error messages";
/// "-S, --show-error": "When used with -s, --silent, it makes curl show an error message if it fails"), run in process
/// through the harness with no reference server, a stand-in process starter and a clock that never moves. The failing
/// transfers are the URL-error checks' own (<see cref="UrlErrorChecks" />), so only the options differ from them.
/// </summary>
internal static class SilentChecks
{
    private const string BadPortReason = "curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535\n";

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>
    /// <c>-s</c> on a malformed URL (a port past 65535) ends with exit 3 and writes nothing at all: no progress meter and
    /// no error message (manual, "-s").
    /// </summary>
    public static Task SilentMalformedAsync(UrlErrorScheme scheme) =>
        Known.RunCaseAsync(
            [Requirement("silent", scheme)],
            () => ExpectAsync(scheme, ["-s", scheme.Url("localhost:99999")], [], 3, string.Empty));

    /// <summary>
    /// <c>-s -S</c> on a malformed URL (a port past 65535) ends with exit 3 and writes curl's error message, and only
    /// that, on standard error (manual, "-S").
    /// </summary>
    public static Task ShowErrorMalformedAsync(UrlErrorScheme scheme) =>
        Known.RunCaseAsync(
            [Requirement("show-error", scheme)],
            () => ExpectAsync(scheme, ["-s", "-S", scheme.Url("localhost:99999")], [], 3, BadPortReason));

    /// <summary>
    /// <c>-s</c> on a transfer to a port no server answers ends with exit 7 and writes nothing at all, though the transfer
    /// started: no progress meter and no error message (manual, "-s").
    /// </summary>
    public static Task SilentRefusedAsync(UrlErrorScheme scheme)
    {
        (string url, Dictionary<string, string> environment) = scheme.Reaching("localhost:1");
        return Known.RunCaseAsync(
            [Requirement("silent-refused", scheme)],
            () => ExpectAsync(scheme, ["-s", url], environment, 7, string.Empty));
    }

    private static string Requirement(string check, UrlErrorScheme scheme) => $"CurlSurface:{check}-{scheme.Scheme}-1";

    private static async Task ExpectAsync(UrlErrorScheme scheme, string[] arguments, Dictionary<string, string> environment, int exitCode, string standardError)
    {
        string directory = Directory.CreateTempSubdirectory("silent-").FullName;
        try
        {
            if (scheme.Helper)
            {
                await File.WriteAllBytesAsync(Path.Combine(directory, "curl-proto-" + scheme.Scheme + (OperatingSystem.IsWindows() ? ".cmd" : string.Empty)), []);
                environment["PATH"] = directory;
            }

            PluginReferenceHelperStarter starter = new(environment, PluginReferenceHelper.Answering(["get"], "ok", "body"));
            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [], arguments, starter, environment, new RealtimeTransferClock())
                .WaitAsync(TimeSpan.FromSeconds(60));

            string commandLine = string.Join(' ', arguments);
            Assert.AreEqual(standardError, Text(result.StandardError), $"{commandLine}: manual, \"-s\" and \"-S\"");
            Assert.AreEqual(exitCode, result.ExitCode, commandLine);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), commandLine);
            Assert.IsEmpty(starter.Starts, $"{commandLine}: no helper program starts");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n");
}
