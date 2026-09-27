using System.Text;

using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>http://</c> and <c>https://</c> end to end through the production composition over
/// a <see cref="ScriptedConnector" />: the request bytes each HTTP option sends and the body
/// written to standard output. Every expected request was measured on 2026-09-26 with
/// curl 8.21.0 (mingw, Schannel) as <c>curl -sS &lt;arguments&gt;</c> against a loopback
/// recorder answering <c>HTTP/1.1 200 OK</c>, <c>Content-Length: 5</c>, <c>hello</c> (BL-231 Notes).
/// </summary>
[TestClass]
public sealed class CurlCompositionHttpTests
{
    private const string Url = "http://127.0.0.1:18231/";

    private const string Head = "Host: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    private const string FormBody = "Content-Type: application/x-www-form-urlencoded\r\n";

    [TestMethod]
    public async Task RunAsync_HttpUrl_SendsCurlsDefaultGetAndWritesTheBodyToStandardOutput()
    {
        (ScriptedConnector server, int exitCode, string standardOutput) = await RunAsync("-sS", "http://127.0.0.1:18231/a?b");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual($"GET /a?b HTTP/1.1\r\n{Head}\r\n", Latin1(server.Written));
        Assert.AreEqual("hello", standardOutput);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18231, false), server.Targets.Single());
    }

    [TestMethod]
    public async Task RunAsync_HttpsUrl_SendsCurlsDefaultGetOverTlsAndWritesTheBodyToStandardOutput()
    {
        (ScriptedConnector server, int exitCode, string standardOutput) = await RunAsync("-sS", "https://localhost:18232/s");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "GET /s HTTP/1.1\r\nHost: localhost:18232\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            Latin1(server.Written));
        Assert.AreEqual("hello", standardOutput);
        Assert.AreEqual(new ConnectTarget("localhost", 18232, true), server.Targets.Single());
    }

    [TestMethod]
    [DataRow(
        new[] { "-X", "PUT", Url },
        $"PUT / HTTP/1.1\r\n{Head}\r\n",
        DisplayName = "-X PUT")]
    [DataRow(
        new[] { "-H", "X-A: 1", "-H", "Accept: text/x", "-H", "User-Agent:", Url },
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nX-A: 1\r\nAccept: text/x\r\n\r\n",
        DisplayName = "-H adds, replaces and removes")]
    [DataRow(
        new[] { "-A", "agent/1", Url },
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nUser-Agent: agent/1\r\nAccept: */*\r\n\r\n",
        DisplayName = "-A agent/1")]
    [DataRow(
        new[] { "-A", "", Url },
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nAccept: */*\r\n\r\n",
        DisplayName = "-A empty")]
    [DataRow(
        new[] { "-e", "http://r/", Url },
        $"GET / HTTP/1.1\r\n{Head}Referer: http://r/\r\n\r\n",
        DisplayName = "-e")]
    [DataRow(
        new[] { "-d", "x=1", Url },
        $"POST / HTTP/1.1\r\n{Head}Content-Length: 3\r\n{FormBody}\r\nx=1",
        DisplayName = "-d x=1")]
    [DataRow(
        new[] { "-d", "a", "-d", "b", Url },
        $"POST / HTTP/1.1\r\n{Head}Content-Length: 3\r\n{FormBody}\r\na&b",
        DisplayName = "-d a -d b")]
    [DataRow(
        new[] { "--data-binary", "a b", Url },
        $"POST / HTTP/1.1\r\n{Head}Content-Length: 3\r\n{FormBody}\r\na b",
        DisplayName = "--data-binary")]
    [DataRow(
        new[] { "--data-raw", "@x", Url },
        $"POST / HTTP/1.1\r\n{Head}Content-Length: 2\r\n{FormBody}\r\n@x",
        DisplayName = "--data-raw")]
    [DataRow(
        new[] { "--data-urlencode", "n=a b", Url },
        $"POST / HTTP/1.1\r\n{Head}Content-Length: 5\r\n{FormBody}\r\nn=a+b",
        DisplayName = "--data-urlencode")]
    [DataRow(
        new[] { "-H", "X: 1", "-d", "a", Url },
        $"POST / HTTP/1.1\r\n{Head}X: 1\r\nContent-Length: 1\r\n{FormBody}\r\na",
        DisplayName = "-H with -d")]
    [DataRow(
        new[] { "-X", "PUT", "-d", "a", "http://127.0.0.1:18231/p" },
        $"PUT /p HTTP/1.1\r\n{Head}Content-Length: 1\r\n{FormBody}\r\na",
        DisplayName = "-X PUT -d")]
    [DataRow(
        new[] { "-G", "-d", "a=1", "-d", "b", "http://127.0.0.1:18231/p?x" },
        $"GET /p?x&a=1&b HTTP/1.1\r\n{Head}\r\n",
        DisplayName = "-G")]
    [DataRow(
        new[] { "-G", "-d", "", Url },
        $"GET / HTTP/1.1\r\n{Head}\r\n",
        DisplayName = "-G with empty data")]
    [DataRow(
        new[] { "-X", "PUT", "-G", "-d", "a", "http://127.0.0.1:18231/p" },
        $"PUT /p?a HTTP/1.1\r\n{Head}\r\n",
        DisplayName = "-X PUT -G")]
    [DataRow(
        new[] { "--url-query", "a b", "http://127.0.0.1:18231/p" },
        $"GET /p?a+b HTTP/1.1\r\n{Head}\r\n",
        DisplayName = "--url-query")]
    [DataRow(
        new[] { "--json", "{\"a\":1}", Url },
        "POST / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\nContent-Type: application/json\r\nAccept: application/json\r\nContent-Length: 7\r\n\r\n{\"a\":1}",
        DisplayName = "--json")]
    [DataRow(
        new[] { "--json", "{}", "-H", "Accept: text/x", Url },
        "POST / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\nAccept: text/x\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}",
        DisplayName = "--json with -H Accept")]
    [DataRow(
        new[] { "--json", "{}", "-H", "Content-Type: text/y", Url },
        "POST / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\nContent-Type: text/y\r\nAccept: application/json\r\nContent-Length: 2\r\n\r\n{}",
        DisplayName = "--json with -H Content-Type")]
    [DataRow(
        new[] { "--json", "{}", "-H", "Accept:", Url },
        "POST / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}",
        DisplayName = "--json with -H Accept: removed")]
    [DataRow(
        new[] { "--json", "{}", "-H", "accept: x", Url },
        "POST / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\naccept: x\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}",
        DisplayName = "--json with lower-case -H accept")]
    [DataRow(
        new[] { "-H", "X: 1", "--json", "a", "-H", "Y: 2", Url },
        "POST / HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\nX: 1\r\nY: 2\r\nContent-Type: application/json\r\nAccept: application/json\r\nContent-Length: 1\r\n\r\na",
        DisplayName = "--json after every -H")]
    [DataRow(
        new[] { "-G", "--json", "a", Url },
        "GET /?a HTTP/1.1\r\nHost: 127.0.0.1:18231\r\nUser-Agent: curl/8.21.0\r\nContent-Type: application/json\r\nAccept: application/json\r\n\r\n",
        DisplayName = "-G --json")]
    public async Task RunAsync_HttpOption_SendsTheRequestCurlSends(string[] arguments, string expectedRequest)
    {
        (ScriptedConnector server, int exitCode, string standardOutput) = await RunAsync(["-sS", .. arguments]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(expectedRequest, Latin1(server.Written));
        Assert.AreEqual("hello", standardOutput);
    }

    /// <summary>
    /// Runs <paramref name="arguments" /> through the production composition over a
    /// <see cref="ScriptedConnector" /> answering <c>200 OK</c> with the body <c>hello</c>.
    /// </summary>
    private static async Task<(ScriptedConnector Server, int ExitCode, string StandardOutput)> RunAsync(params string[] arguments)
    {
        ScriptedConnector server = new([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(
                standardOutput,
                standardError,
                standardInput,
                server,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(arguments);

        return (server, exitCode, Encoding.Latin1.GetString(standardOutput.ToArray()));
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
