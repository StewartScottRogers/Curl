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
