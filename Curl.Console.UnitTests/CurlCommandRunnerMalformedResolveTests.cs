using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Engine;
using Curl.Original.Protocol.Http;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that a malformed <c>--resolve</c> entry exits 49 as each transfer starts, as curl 8.22.0 does: it loads the
/// list before it parses the URL (AF-0807) and before it looks for a connection to reuse (STD-225, BL-6022,
/// measured with <c>Record-CurlExchange.ps1</c> on 2026-11-26).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerMalformedResolveTests
{
    private const string Expected = "curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:'";

    [TestMethod]
    [DataRow("/")]
    [DataRow("http://")]
    [DataRow(":1")]
    public async Task RunAsync_MalformedResolveWithRejectedUrl_Exits49(string url)
    {
        (int exitCode, string error) = await RunAsync("-sS", "--resolve", "x:", url);

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual(Expected, error.TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_MalformedResolveWithRejectedQueryUrl_Exits3AsCurlsToolFailsTheQueryFirst()
    {
        (int exitCode, string error) = await RunAsync("-sS", "-G", "--url-query", "a=b", "--resolve", "x:", "http://a b/");

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("curl: (3) Could not parse the URL, failed to set query", error.TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_VerboseMalformedResolveAfterGoodEntry_PrintsAddedLineAndEntryInfoThenExits49()
    {
        (int exitCode, string error) = await RunAsync("-v", "--resolve", "good:80:127.0.0.1", "--resolve", "x:", "/");

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual("* [DNS] added good:80:127.0.0.1 to cache\n* Could not parse CURLOPT_RESOLVE entry 'x:'\n" + Expected, error.Replace("\r\n", "\n").TrimEnd());
    }

    [TestMethod]
    public async Task RunAsync_WellFormedResolveWithRejectedUrl_StillExits3()
    {
        (int exitCode, _) = await RunAsync("-sS", "--resolve", "x:80:127.0.0.1", "/");

        Assert.AreEqual(3, exitCode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_MalformedResolveInALaterGroupOnAReusableConnection_Exits49WithoutSendingTheRequest(bool parallel)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        MemoryStream standardOutput = new();
        string[] arguments =
        [
            .. parallel ? new[] { "-Z" } : [],
            "-sS", "http://127.0.0.1:24000/hello",
            "--next", "-sS", "--resolve", "bad", "http://127.0.0.1:24000/json", "-w", "[%{exitcode} %{num_connects} %{conn_id}]\n",
        ];

        (int exitCode, string error) = await RunAsync(server, standardOutput, arguments);

        Assert.AreEqual(49, exitCode);
        Assert.AreEqual("curl: (49) Could not parse CURLOPT_RESOLVE entry 'bad'", error.TrimEnd());
        string output = Encoding.Latin1.GetString(standardOutput.ToArray());
        // Under -Z the transfers report in completion order, so only the two outputs themselves are pinned.
        Assert.AreEqual("hello", output.Replace("[49 0 -1]\n", string.Empty, StringComparison.Ordinal));
        StringAssert.Contains(output, "[49 0 -1]\n");
        Assert.HasCount(1, server.Targets);
    }

    private static Task<(int ExitCode, string Error)> RunAsync(params string[] arguments) =>
        RunAsync(new ScriptedConnector([]), new MemoryStream(), arguments);

    private static async Task<(int ExitCode, string Error)> RunAsync(ScriptedConnector server, MemoryStream standardOutput, string[] arguments)
    {
        MemoryStream standardError = new();
        InMemoryFileSystem files = new();
        HttpProtocolHandler http = new(server, new Curl.Authentication.BasicAndBearerAuthenticator(Curl.Authentication.CredentialEncoding.ForPlatform(isWindows: false)));
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http]), [], loadResolveEntries: events => events.ReportInfo("[DNS] added good:80:127.0.0.1 to cache")),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false)
            .RunAsync(arguments);

        return (exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
    }
}
