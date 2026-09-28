using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http;

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

    [TestMethod]
    public async Task RunAsync_SkipExistingFilePresent_ConnectsToNothingAndPrintsNothing()
    {
        outputFiles.ExistingPaths.Add("out.txt");

        int exitCode = await RunAsync([Ok], "-o", "out.txt", "--skip-existing", Url);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(server.Targets);
        Assert.IsEmpty(outputFiles.WriteModes);
        Assert.AreEqual(string.Empty, StandardOutputText);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    [DataRow("-v")]
    [DataRow("-sv")]
    public async Task RunAsync_SkipExistingFilePresentVerbose_PrintsTheWrappedNote(string verbose)
    {
        outputFiles.ExistingPaths.Add(MeasuredPath);

        int exitCode = await RunAsync([Ok], verbose, "-o", MeasuredPath, "--skip-existing", Url);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(server.Targets);
        Assert.AreEqual(
            @"Note: skips transfer, ""C:\Users\Stewart " + NewLine
            + @"Note: Rogers\AppData\Local\Temp\bl493-2fd6e5\out.txt"" exists locally" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SkipExistingFilePresentWithWriteOut_WritesTheSkippedTransfersVariables()
    {
        outputFiles.ExistingPaths.Add("out.txt");

        int exitCode = await RunAsync(
            [Ok], "-s", "-o", "out.txt", "--skip-existing", "-w", "[%{http_code}|%{filename_effective}|%{exitcode}|%{url}|%{num_connects}]", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("[000|out.txt|0|" + Url + "|0]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_SkipExistingFileAbsent_Transfers()
    {
        int exitCode = await RunAsync([Ok], "-s", "-o", "out.txt", "--skip-existing", Url);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, server.Targets);
        Assert.AreEqual("hello", WrittenText("out.txt"));
    }

    [TestMethod]
    public async Task RunAsync_SkipExistingOnlyFirstFilePresent_SkipsItAndTransfersTheSecond()
    {
        outputFiles.ExistingPaths.Add("one.txt");

        int exitCode = await RunAsync(
            [Ok], "-s", "--skip-existing", "-o", "one.txt", "http://127.0.0.1:18493/1", "-o", "two.txt", "http://127.0.0.1:18493/2");

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, server.Targets);
        StringAssert.StartsWith(Encoding.Latin1.GetString(server.Written), "GET /2 HTTP/1.1");
        CollectionAssert.AreEquivalent(new[] { "two.txt" }, outputFiles.Written.Keys);
        Assert.AreEqual("hello", WrittenText("two.txt"));
    }

    [TestMethod]
    public async Task RunAsync_SkipExistingRemoteNameUnderOutputDirectoryPresent_Skips()
    {
        outputFiles.ExistingPaths.Add("d/a");

        int exitCode = await RunAsync([Ok], "-s", "-O", "--output-dir", "d", "--skip-existing", "-w", "%{filename_effective}", Url);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(server.Targets);
        Assert.AreEqual("d/a", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NoSkipExistingAfterSkipExisting_Overwrites()
    {
        outputFiles.ExistingPaths.Add("out.txt");

        int exitCode = await RunAsync([Ok], "-s", "-o", "out.txt", "--skip-existing", "--no-skip-existing", Url);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", WrittenText("out.txt"));
    }

    private string WrittenText(string path) => Encoding.Latin1.GetString(outputFiles.Written[path].ToArray());

    private Task<int> RunAsync(string[] responses, params string[] arguments)
    {
        server = new ScriptedConnector(responses.Select(Encoding.Latin1.GetBytes));
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        return new CurlCommandRunner(
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
}
