using Curl.Authentication;
using Curl.Original.Protocol.Http;
using Curl.Testing;
using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that <c>-O</c> and <c>--remote-name-all</c> end the whole run, with curl's "Failed to extract a
/// filename" line, when the URL yields no file name: exit 3 for a URL the URL API rejects and exit 1 for a
/// scheme it does not support (BL-5016, measured on curl 8.22.0 by the 2026-11-12 fitness review).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRemoteNameExtractionFailureTests
{
    private const string ExtractionLine = "curl: Failed to extract a filename from the URL to use for storage";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    [DataRow("-O")]
    [DataRow("--remote-name-all")]
    public async Task RunAsync_RemoteNameOnUrlWithASpaceBeforeAGoodUrl_StopsWithExit3AndFetchesNothing(string option)
    {
        int exitCode = await RunAsync([option, "http://h/f/a b", "http://h/f/good.txt"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(
            ExtractionLine + NewLine + "curl: (3) URL using bad/illegal format or missing URL" + NewLine,
            StandardErrorText);
        Assert.IsEmpty(http.Contexts);
        Assert.IsEmpty(fileSystem.Written);
    }

    [TestMethod]
    public async Task RunAsync_SingleRemoteNameOnUrlWithASpace_PrintsTheSameTwoLinesAndExit3()
    {
        int exitCode = await RunAsync(["-O", "http://h/f/a b"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(
            ExtractionLine + NewLine + "curl: (3) URL using bad/illegal format or missing URL" + NewLine,
            StandardErrorText);
        Assert.IsEmpty(fileSystem.Written);
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameOnUnsupportedSchemeBeforeAGoodUrl_StopsWithExit1()
    {
        int exitCode = await RunAsync(["-O", "foo://x/z", "-O", "http://h/f/good.txt"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(ExtractionLine + NewLine + "curl: (1) Unsupported protocol" + NewLine, StandardErrorText);
        Assert.IsEmpty(http.Contexts);
        Assert.IsEmpty(fileSystem.Written);
    }

    [TestMethod]
    public async Task RunAsync_SilentRemoteNameOnUrlWithASpace_PrintsNothingAndExits3()
    {
        int exitCode = await RunAsync(["-s", "-O", "http://h/f/a b"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameOnSeveralGoodUrls_SavesEachFileAndExits0()
    {
        int exitCode = await RunAsync(["-O", "http://h/f/good.txt", "-O", "http://h/f/other.txt"]);

        Assert.AreEqual(0, exitCode);
        Assert.IsTrue(fileSystem.Written.ContainsKey("good.txt"));
        Assert.IsTrue(fileSystem.Written.ContainsKey("other.txt"));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_RemoteHeaderNameThatIsBlank_PrintsTheExtractionLineAndExits3()
    {
        string response = "HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename=\" \"\r\nContent-Length: 5\r\n\r\nhello";
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(response)]);
        HttpProtocolHandler scripted = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([scripted])),
            fileSystem,
            fileSystem,
            standardOutput,
            standardError,
            new MemoryStream(),
            false,
            outputPaths: fileSystem);

        int exitCode = await runner.RunAsync(["-OJ", "http://127.0.0.1:18239/u.txt"]);

        Assert.AreEqual(
            ExtractionLine + NewLine + "curl: (3) URL using bad/illegal format or missing URL" + NewLine,
            StandardErrorText);
        Assert.AreEqual(3, exitCode);
        Assert.IsEmpty(fileSystem.Written);
    }

    [TestMethod]
    public async Task RunAsync_ParallelTransfersOneWithABlankRemoteHeaderName_RefusesOnlyThatTransfer()
    {
        HostKeyedConnector server = new(new Dictionary<string, byte[]>
        {
            ["blank.test"] = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename=\" \"\r\nContent-Length: 5\r\n\r\nhello"),
            ["good.test"] = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename=\"kept.txt\"\r\nContent-Length: 5\r\n\r\nworld"),
        });

        int exitCode = await RunScriptedAsync(server, ["-Z", "-J", "-O", "http://good.test/v.txt", "-J", "-O", "http://blank.test/u.txt"]);

        Assert.AreEqual(3, exitCode);
        Assert.IsTrue(fileSystem.Written.ContainsKey("kept.txt"));
        Assert.HasCount(1, fileSystem.Written);
        Assert.Contains(ExtractionLine, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_BlankRemoteHeaderNameUnderRetryAllErrors_IsNotRetried()
    {
        HostKeyedConnector server = new(new Dictionary<string, byte[]>
        {
            ["blank.test"] = Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Disposition: attachment; filename=\" \"\r\nContent-Length: 5\r\n\r\nhello"),
        });

        int exitCode = await RunScriptedAsync(server, ["--retry", "2", "--retry-all-errors", "-J", "-O", "http://blank.test/u.txt"]);

        Assert.AreEqual(3, exitCode);
        Assert.DoesNotContain("retry", StandardErrorText, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(1, server.Connects);
    }

    private async Task<int> RunScriptedAsync(IConnector server, IReadOnlyList<string> arguments)
    {
        HttpProtocolHandler scripted = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([scripted])),
            fileSystem,
            fileSystem,
            standardOutput,
            standardError,
            new MemoryStream(),
            false,
            outputPaths: fileSystem);

        return await runner.RunAsync(arguments);
    }

    private sealed class HostKeyedConnector(Dictionary<string, byte[]> responses) : IConnector
    {
        private int connects;

        public int Connects => Volatile.Read(ref connects);

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref connects);
            return new ScriptedConnector([responses[target.Host]]).ConnectAsync(target, cancellationToken);
        }
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([http])),
            fileSystem,
            fileSystem,
            standardOutput,
            standardError,
            new MemoryStream(),
            false,
            outputPaths: fileSystem,
            configFileReader: new InMemoryDataFileReader());

        return await runner.RunAsync(arguments);
    }
}
