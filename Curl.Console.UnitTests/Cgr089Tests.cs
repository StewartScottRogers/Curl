using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-089 (BL-3618): <c>%{url}</c> and <c>%{url.query}</c> print the URL with the query <c>-G -d</c> or
/// <c>--url-query</c> appended, as curl's tool stores the URL it rebuilt to append it: normalised as
/// <c>%{url_effective}</c> is, without a scheme the user did not type, and as typed, without the query, when curl
/// rejects it. Measured on curl 8.21.0 (Windows, Schannel) on 2026-10-26; the commands are in BL-3618's Notes.
/// </summary>
[TestClass]
public sealed class Cgr089Tests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http://127.0.0.1:1/p", "-G", "-d", "a=b", "http://127.0.0.1:1/p?a=b|a=b")]
    [DataRow("http://127.0.0.1:1/p?y=2", "--url-query", "x=1", "", "http://127.0.0.1:1/p?y=2&x=1|y=2&x=1")]
    [DataRow("HTTP://127.0.0.1:1/a/../p#frag", "-G", "-d", "a=%2f", "http://127.0.0.1:1/p?a=%2F#frag|a=%2F")]
    [DataRow("http://127.0.0.1:1", "-G", "-d", "a=b", "http://127.0.0.1:1/?a=b|a=b")]
    [DataRow("LocalHost:1/x/../p#f", "-G", "-d", "a=b", "LocalHost:1/p?a=b#f|a=b")]
    [DataRow("HTTP://127.0.0.1:1/a/../p", "", "", "", "HTTP://127.0.0.1:1/a/../p|")]
    public async Task RunAsync_QueryAppendedByOption_UrlVariablesPrintTheUrlWithIt(
        string url, string option, string argument, string value, string expected)
    {
        string[] queryOptions = new[] { option, argument, value }.Where(text => text.Length > 0).ToArray();

        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{url}|%{url.query}", .. queryOptions, url]);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("stdout", expected, StandardOutputText);
        Assert.AreEqual(expected, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_QueryAppendedToARejectedUrl_UrlPrintsItAsTypedWithoutTheQuery()
    {
        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{url}|%{url.query}", "-G", "-d", "a=b", "http://a b/p"]);

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Assert("stdout", "http://a b/p|", StandardOutputText);
        Assert.AreEqual("http://a b/p|", StandardOutputText);
    }

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([http])),
            fileSystem,
            fileSystem,
            standardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: false,
            outputPaths: fileSystem);

        int exitCode = await runner.RunAsync(arguments);

        Diagnostics.Act("stderr", Encoding.UTF8.GetString(standardError.ToArray()));
        return exitCode;
    }
}
