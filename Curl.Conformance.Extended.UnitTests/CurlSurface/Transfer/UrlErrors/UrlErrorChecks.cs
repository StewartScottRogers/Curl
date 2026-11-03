using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Plugin;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>
/// curl's URL errors checked for one extended scheme against curl's manual page for curl 8.22.0
/// (curl.se/docs/manpage.html, "EXIT CODES" 3, 6 and 7), with the error text curl 8.22.0 writes for an <c>http://</c>
/// URL of the same shape (measured with <c>Record-CurlExchange.ps1</c>), run in process through the harness with no
/// reference server, a stand-in process starter and a clock that never moves.
/// </summary>
internal static class UrlErrorChecks
{
    /// <summary>The malformed-URL check that puts a port past 65535 in the URL.</summary>
    public const string BadPort = "url-bad-port";

    /// <summary>The malformed-URL check that puts a percent sign not followed by two hex digits in the host.</summary>
    public const string BadPercentEncoding = "url-bad-percent-encoding";

    /// <summary>The malformed-URL check that puts a space in the host.</summary>
    public const string Space = "url-space";

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    /// <summary>A malformed URL ends with exit 3, "URL malformed. The syntax was not correct" (manual, "EXIT CODES" 3), and curl's reason.</summary>
    public static Task MalformedAsync(UrlErrorScheme scheme, string check)
    {
        (string authority, string reason) = check switch
        {
            BadPort => ("localhost:99999", "Port number was not a decimal number between 0 and 65535"),
            BadPercentEncoding => ("local%zzhost", "Bad hostname"),
            _ => ("local host", "Malformed input to a URL function"),
        };
        return Known.RunCaseAsync(
            [scheme.Requirement(check)],
            () => ExpectAsync(scheme, scheme.Url(authority), [], 3, $"curl: (3) URL rejected: {reason}\n"));
    }

    /// <summary>A host under <c>.invalid</c>, which never resolves, ends with exit 6, "Could not resolve host" (manual, "EXIT CODES" 6).</summary>
    public static Task UnresolvableHostAsync(UrlErrorScheme scheme)
    {
        (string url, Dictionary<string, string> environment) = scheme.Reaching("nohost.invalid");
        return Known.RunCaseAsync(
            [scheme.Requirement("unresolvable-host")],
            () => ExpectAsync(scheme, url, environment, 6, "curl: (6) Could not resolve host: nohost.invalid\n"));
    }

    /// <summary>
    /// A port no server answers ends with exit 7, "Failed to connect to host" (manual, "EXIT CODES" 7); over TCP with curl
    /// 8.22.0's text, after 0 ms on a clock that never moves; over UDP with the harness's datagram connector's reason.
    /// </summary>
    public static Task ConnectionRefusedAsync(UrlErrorScheme scheme)
    {
        (string url, Dictionary<string, string> environment) = scheme.Reaching("localhost:1");
        string expected = scheme.Datagram
            ? "curl: (7) No reference server answers UDP port 1\n"
            : $"curl: (7) Failed to connect to {scheme.RefusedTarget} after 0 ms: Could not connect to server\n";
        return Known.RunCaseAsync(
            [scheme.Requirement("connection-refused")],
            () => ExpectAsync(scheme, url, environment, 7, expected));
    }

    private static async Task ExpectAsync(UrlErrorScheme scheme, string url, Dictionary<string, string> environment, int exitCode, string standardError)
    {
        string directory = Directory.CreateTempSubdirectory("url-errors-").FullName;
        try
        {
            if (scheme.Helper)
            {
                await File.WriteAllBytesAsync(Path.Combine(directory, "curl-proto-" + scheme.Scheme + (OperatingSystem.IsWindows() ? ".cmd" : string.Empty)), []);
                environment["PATH"] = directory;
            }

            PluginReferenceHelperStarter starter = new(environment, PluginReferenceHelper.Answering(["get"], "ok", "body"));
            CurlRunResult result = await ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [], ["-sS", url], starter, environment, new RealtimeTransferClock())
                .WaitAsync(TimeSpan.FromSeconds(60));

            Assert.AreEqual(standardError, Text(result.StandardError), $"{url}: manual, EXIT CODES {exitCode}");
            Assert.AreEqual(exitCode, result.ExitCode, url);
            Assert.AreEqual(string.Empty, Text(result.StandardOutput), url);
            Assert.IsEmpty(starter.Starts, $"{url}: no helper program starts");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes).ReplaceLineEndings("\n");
}
