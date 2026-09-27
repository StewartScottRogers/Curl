using System.Text;

using Curl.Cli;
using Curl.Core;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins how <c>-T</c> / <c>--upload-file</c> transfers are dispatched: each <c>-T</c> pairs with a
/// URL in order, its URL is resolved by <see cref="UploadTransferUrl" /> before the file is opened,
/// and the opened file is the transfer's <see cref="ITransferContext.Upload" />. Every expected
/// line and exit code was measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27 (BL-030 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerUploadTests
{
    private static readonly string NewLine = Environment.NewLine;

    private readonly InMemoryFileSystem files = new();

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly MemoryStream standardInput = new([1, 2, 3]);

    private readonly List<(string Url, byte[]? Upload)> dispatched = [];

    public CurlCommandRunnerUploadTests()
    {
        files.ExistingContent["a"] = Encoding.ASCII.GetBytes("A");
        files.ExistingContent["b"] = Encoding.ASCII.GetBytes("B");
        files.ExistingContent["local.txt"] = Encoding.ASCII.GetBytes("local");
        files.UnreadablePaths.Add("nosuchfile");
    }

    [TestMethod]
    public async Task RunAsync_TwoUploadsAndTwoDirectoryUrls_SendsEachFileToItsOwnUrlInOrder()
    {
        int exitCode = await RunAsync("-T", "a", "-T", "b", "http://h/1/", "http://h/2/");

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(2, dispatched);
        Assert.AreEqual("http://h/1/a", dispatched[0].Url);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("A"), dispatched[0].Upload);
        Assert.AreEqual("http://h/2/b", dispatched[1].Url);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("B"), dispatched[1].Upload);
    }

    [TestMethod]
    public async Task RunAsync_UploadGivenAfterTheUrls_StillPairsInOrder()
    {
        await RunAsync("http://h/1/", "-T", "a", "http://h/2/", "-T", "b");

        Assert.AreEqual("http://h/1/a", dispatched[0].Url);
        Assert.AreEqual("http://h/2/b", dispatched[1].Url);
    }

    [TestMethod]
    public async Task RunAsync_MoreUrlsThanUploads_SendsTheLaterUrlUnchangedWithNoUpload()
    {
        await RunAsync("-T", "a", "http://h/1/", "http://h/2/");

        Assert.AreEqual("http://h/2/", dispatched[1].Url);
        Assert.IsNull(dispatched[1].Upload);
    }

    [TestMethod]
    [DataRow("-")]
    [DataRow(".")]
    public async Task RunAsync_UploadFromStandardInput_SendsStandardInputToTheUrlUnchanged(string uploadFile)
    {
        int exitCode = await RunAsync("-T", uploadFile, "http://h/d/");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("http://h/d/", dispatched.Single().Url);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, dispatched.Single().Upload);
        Assert.IsEmpty(files.ReadPaths);
    }

    [TestMethod]
    public async Task RunAsync_EmptyUpload_SendsTheUrlUnchangedWithNoUpload()
    {
        await RunAsync("-T", string.Empty, "-T", "a", "http://h/1/", "http://h/2/");

        Assert.AreEqual("http://h/1/", dispatched[0].Url);
        Assert.IsNull(dispatched[0].Upload);
        Assert.AreEqual("http://h/2/a", dispatched[1].Url);
    }

    [TestMethod]
    public async Task RunAsync_UploadToMalformedUrl_Exits3WithoutOpeningTheFile()
    {
        int exitCode = await RunAsync("-T", "nosuchfile", "http://h/d ir/");

        Assert.AreEqual((int)CurlExitCode.UrlMalformat, exitCode);
        Assert.IsEmpty(files.ReadPaths);
        Assert.IsEmpty(dispatched);
        Assert.AreEqual(
            $"curl: (3) URL using bad/illegal format or missing URL{NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadToMalformedUrl_PrintsNoWarningLinesAndAnEmptyEffectiveUrl()
    {
        int exitCode = await RunWithWarningLinesAsync("-s", "-T", "a", "-w", "[%{url_effective}]", "http://h/d ir/");

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual("[]", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadFileThatCannotBeOpened_Exits26AndStopsTheRun()
    {
        int exitCode = await RunAsync("-T", "nosuchfile", "-T", "a", "http://h/d/", "http://h/2/");

        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Assert.IsEmpty(dispatched);
        Assert.AreEqual(
            "curl: cannot open 'nosuchfile'" + NewLine
            + CommandLineRefusal.TryHelpLine + NewLine
            + $"curl: (26) {MultipartFormBodyBuilder.OpenFailedMessage}{NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_SilentUploadFileThatCannotBeOpened_StillPrintsTheCannotOpenLines()
    {
        int exitCode = await RunAsync("-s", "-T", "nosuchfile", "-w", "%{url_effective}", "http://h/d/");

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual(
            "curl: cannot open 'nosuchfile'" + NewLine + CommandLineRefusal.TryHelpLine + NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual("http://h/d/nosuchfile", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadFileThatCannotBeOpened_PrintsTheWarningLinesFirst()
    {
        await RunWithWarningLinesAsync("-T", "nosuchfile", "http://h/d/");

        StringAssert.StartsWith(
            Encoding.UTF8.GetString(standardError.ToArray()),
            "Warning: w" + NewLine + "curl: cannot open 'nosuchfile'");
    }

    [TestMethod]
    [DataRow("http:/h", "http://h/local.txt")]
    [DataRow("h/dir/", "http://h/dir/local.txt")]
    public async Task RunAsync_UploadToUrlNeedingNormalisation_DispatchesAndReportsTheNormalisedUrl(string url, string expected)
    {
        await RunAsync("-T", "local.txt", "-w", "%{url_effective}", url);

        Assert.AreEqual(expected, dispatched.Single().Url);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadWithOutputFile_SendsTheFileAndWritesTheOutputFile()
    {
        int exitCode = await RunAsync("-T", "a", "-o", "out", "http://h/1/");

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("A"), dispatched.Single().Upload);
        Assert.IsTrue(files.Written.ContainsKey("out"));
    }

    [TestMethod]
    public async Task RunAsync_UploadWithForm_SendsTheFile()
    {
        await RunAsync("-T", "a", "-F", "x=y", "http://h/1/");

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("A"), dispatched.Single().Upload);
    }

    [TestMethod]
    public async Task RunAsync_UploadWithHeaderFile_SendsTheFile()
    {
        await RunAsync("-T", "a", "-D", "headers", "http://h/1/");

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("A"), dispatched.Single().Upload);
    }

    private RecordingProtocolHandler CreateHandler() =>
        new("http", async context =>
        {
            byte[]? upload = null;
            if (context.Upload is { } source)
            {
                using MemoryStream copy = new();
                await source.CopyToAsync(copy, context.CancellationToken);
                upload = copy.ToArray();
            }

            dispatched.Add((UrlText(context.Url), upload));
            return TransferResult.Success(0);
        });

    private static string UrlText(CurlUrl url) =>
        $"{url.Scheme}://{url.Host}{url.AbsolutePath}";

    private Task<int> RunAsync(params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([CreateHandler()])),
                files,
                files,
                standardOutput,
                standardError,
                standardInput,
                runsOnWindows: false,
                formBodyBuilder: new MultipartFormBodyBuilder(files, Encoding.UTF8, () => "b"))
            .RunAsync(arguments);

    private Task<int> RunWithWarningLinesAsync(params string[] arguments) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([CreateHandler()]), ["Warning: w"]),
                files,
                files,
                standardOutput,
                standardError,
                standardInput,
                runsOnWindows: false)
            .RunAsync(arguments);
}
