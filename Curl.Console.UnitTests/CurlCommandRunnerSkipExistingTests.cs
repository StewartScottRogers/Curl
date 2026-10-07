using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>--skip-existing</c> end to end through the runner and <see cref="HttpProtocolHandler" />
/// over a <see cref="ScriptedConnector" />, with the output files in an
/// <see cref="InMemoryFileSystem" />. Every expectation was measured on 2026-09-28 with curl 8.21.0
/// (mingw, Schannel) against a loopback server answering with a 5-byte <c>hello</c> body (BL-493
/// Notes): a transfer whose output file exists makes no connection, writes nothing, draws no
/// progress meter and exits 0, printing a note only under <c>-v</c>, and the next URL still runs.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerSkipExistingTests
{
    private const string Url = "http://127.0.0.1:18493/a";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string MeasuredPath = @"C:\Users\Stewart Rogers\AppData\Local\Temp\bl493-2fd6e5\out.txt";

    private static readonly string NewLine = Environment.NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private ScriptedConnector server = new([]);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    [TestMethod]
    public async Task RunAsync_SkipExistingFilePresent_ConnectsToNothingAndPrintsNothing()
    {
        outputFiles.ExistingPaths.Add("out.txt");

        int exitCode = await RunAsync([Ok], "-o", "out.txt", "--skip-existing", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("connection count", 0, server.Targets.Count);
        Assert.IsEmpty(server.Targets);
        Diagnostics.Assert("write count", 0, outputFiles.WriteModes.Count);
        Assert.IsEmpty(outputFiles.WriteModes);
        Diagnostics.Diff("stdout", string.Empty, StandardOutputText);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Diagnostics.Diff("stderr", string.Empty, Lf(StandardErrorText));
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-sv")]
    public async Task RunAsync_SkipExistingFilePresentVerbose_PrintsTheWrappedNote(string verbose)
    {
        outputFiles.ExistingPaths.Add(MeasuredPath);

        int exitCode = await RunAsync([Ok], verbose, "-o", MeasuredPath, "--skip-existing", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("connection count", 0, server.Targets.Count);
        Assert.IsEmpty(server.Targets);
        string expectedNote =
            @"Note: skips transfer, ""C:\Users\Stewart " + NewLine
            + @"Note: Rogers\AppData\Local\Temp\bl493-2fd6e5\out.txt"" exists locally" + NewLine;
        Diagnostics.Diff("stderr", Lf(expectedNote), Lf(StandardErrorText));
        Assert.AreEqual(
            expectedNote,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SkipExistingFilePresentWithWriteOut_WritesTheSkippedTransfersVariables()
    {
        outputFiles.ExistingPaths.Add("out.txt");

        int exitCode = await RunAsync(
            [Ok], "-s", "-o", "out.txt", "--skip-existing", "-w", "[%{http_code}|%{filename_effective}|%{exitcode}|%{url}|%{num_connects}]", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("stdout", "[000|out.txt|0|" + Url + "|0]", StandardOutputText);
        Assert.AreEqual("[000|out.txt|0|" + Url + "|0]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_SkipExistingFileAbsent_Transfers()
    {
        int exitCode = await RunAsync([Ok], "-s", "-o", "out.txt", "--skip-existing", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("connection count", 1, server.Targets.Count);
        Assert.HasCount(1, server.Targets);
        Diagnostics.Diff("out.txt" + " content", "hello", WrittenText("out.txt"));
        Assert.AreEqual("hello", WrittenText("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_SkipExistingOnlyFirstFilePresent_SkipsItAndTransfersTheSecond()
    {
        outputFiles.ExistingPaths.Add("one.txt");

        int exitCode = await RunAsync(
            [Ok], "-s", "--skip-existing", "-o", "one.txt", "http://127.0.0.1:18493/1", "-o", "two.txt", "http://127.0.0.1:18493/2");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("connection count", 1, server.Targets.Count);
        Assert.HasCount(1, server.Targets);
        Diagnostics.Assert("request starts with GET /2", true, Encoding.Latin1.GetString(server.Written).StartsWith("GET /2 HTTP/1.1", StringComparison.Ordinal));
        StringAssert.StartsWith(Encoding.Latin1.GetString(server.Written), "GET /2 HTTP/1.1");
        Diagnostics.Assert("files written", "two.txt", string.Join(", ", outputFiles.Written.Keys.Order(StringComparer.Ordinal)));
        CollectionAssert.AreEquivalent(new[] { "two.txt" }, outputFiles.Written.Keys);
        Diagnostics.Diff("two.txt" + " content", "hello", WrittenText("two.txt"));
        Assert.AreEqual("hello", WrittenText("two.txt"));
    }

    [TestMethod]
    public async Task RunAsync_SkipExistingRemoteNameUnderOutputDirectoryPresent_Skips()
    {
        outputFiles.ExistingPaths.Add("d/a");

        int exitCode = await RunAsync([Ok], "-s", "-O", "--output-dir", "d", "--skip-existing", "-w", "%{filename_effective}", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("connection count", 0, server.Targets.Count);
        Assert.IsEmpty(server.Targets);
        Diagnostics.Diff("stdout", "d/a", StandardOutputText);
        Assert.AreEqual("d/a", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NoSkipExistingAfterSkipExisting_Overwrites()
    {
        outputFiles.ExistingPaths.Add("out.txt");

        int exitCode = await RunAsync([Ok], "-s", "-o", "out.txt", "--skip-existing", "--no-skip-existing", Url);

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("out.txt" + " content", "hello", WrittenText("out.txt"));
        Assert.AreEqual("hello", WrittenText("out.txt"));
    }

    private string WrittenText(string path) => Encoding.Latin1.GetString(outputFiles.Written[path].ToArray());

    private async Task<int> RunAsync(string[] responses, params string[] arguments)
    {
        server = new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("existing output paths", string.Join(", ", outputFiles.ExistingPaths.Order(StringComparer.Ordinal)));
        Diagnostics.Arrange("connector script", Lf(string.Join("\n--next response--\n", responses)));
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(
                    _ => new TransferDispatch(new ProtocolDispatcher([http])),
                    outputFiles,
                    outputFiles,
                    standardOutput,
                    standardError,
                    new MemoryStream(),
                    runsOnWindows: false,
                    outputPaths: outputFiles)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("connections made", server.Targets.Count);
        Diagnostics.Act("stdout", Lf(StandardOutputText));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        return exitCode;
    }
}
