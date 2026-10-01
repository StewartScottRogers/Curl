using System.Text;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins <c>--libcurl</c> through the runner against curl 8.21.0 (mingw, Schannel) measured on 2026-10-01
/// (BL-652 Notes): the transfers still run, the source follows their output on standard output for
/// <c>-</c>, a file gets it in CR LF on Windows and LF elsewhere, and a file that cannot be opened gets
/// curl's warning unless <c>-s</c> was given.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerLibcurlTests
{
    private const string SourceFile = "out.c";

    private const string Url = "http://localhost:47652/";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    private string StandardErrorText => Encoding.Latin1.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_LibcurlToStandardOutput_WritesTheSourceAfterTheTransfersBody()
    {
        int exitCode = await RunAsync(["-s", "--libcurl", "-", Url]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello" + ExpectedSource(), StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_LibcurlToAFileOnWindows_WritesItInCrLfAndPerformsTheTransfer()
    {
        int exitCode = await RunAsync(["-s", "--libcurl", SourceFile, Url]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.AreEqual(ExpectedSource().Replace("\n", "\r\n", StringComparison.Ordinal), WrittenSource());
    }

    [TestMethod]
    public async Task RunAsync_LibcurlToAFileOffWindows_WritesItInLineFeeds()
    {
        int exitCode = await RunAsync(["-s", "--libcurl", SourceFile, Url], runsOnWindows: false);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(ExpectedSource(), WrittenSource());
    }

    [TestMethod]
    public async Task RunAsync_FileThatCannotBeOpened_WarnsAsCurlDoes()
    {
        files.UnwritablePaths.Add(SourceFile);

        int exitCode = await RunAsync(["--no-progress-meter", "--libcurl", SourceFile, Url]);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Warning: Failed to open out.c to write libcurl code" + Environment.NewLine, StandardErrorText);
        Assert.IsFalse(files.Written.ContainsKey(SourceFile));
    }

    [TestMethod]
    public async Task RunAsync_FileThatCannotBeOpenedUnderSilent_SaysNothing()
    {
        files.UnwritablePaths.Add(SourceFile);

        int exitCode = await RunAsync(["-s", "--libcurl", SourceFile, Url]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_WithoutLibcurl_WritesNoSource()
    {
        int exitCode = await RunAsync(["-s", Url]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", StandardOutputText);
        Assert.IsEmpty(files.Written);
    }

    private static string ExpectedSource()
    {
        CommandLineOptions options = CommandLineParser.Parse(["-s", Url]).Options!;
        return LibcurlSourceCode.Generate([(options, Url)]);
    }

    private string WrittenSource() => Encoding.UTF8.GetString(files.Written[SourceFile].ToArray());

    private Task<int> RunAsync(string[] arguments, bool runsOnWindows = true) =>
        new CurlCommandRunner(
                options => CreateTransferDispatch(),
                files,
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: runsOnWindows)
            .RunAsync(arguments);

    private static TransferDispatch CreateTransferDispatch() =>
        new(new ProtocolDispatcher(CurlComposition.CreateProtocolHandlers(
            new ScriptedConnector([Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")]),
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver())));
}
