using System.Text;

using Curl.Cli;
using Curl.Core;
using Curl.Core.Multipart;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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
        files.ExistingContent["sub/in.txt"] = Encoding.ASCII.GetBytes("in");
        files.UnreadablePaths.Add("nosuchfile");
    }

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.UTF8.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_TwoUploadsAndTwoDirectoryUrls_SendsEachFileToItsOwnUrlInOrder()
    {
        int exitCode = await RunAsync("-T", "a", "-T", "b", "http://h/1/", "http://h/2/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("dispatched count", 2, dispatched.Count);
        Assert.HasCount(2, dispatched);
        Diagnostics.Assert("first URL", "http://h/1/a", dispatched[0].Url);
        Assert.AreEqual("http://h/1/a", dispatched[0].Url);
        Diagnostics.Diff("first upload", Encoding.ASCII.GetBytes("A"), dispatched[0].Upload);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("A"), dispatched[0].Upload);
        Diagnostics.Assert("second URL", "http://h/2/b", dispatched[1].Url);
        Assert.AreEqual("http://h/2/b", dispatched[1].Url);
        Diagnostics.Diff("second upload", Encoding.ASCII.GetBytes("B"), dispatched[1].Upload);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("B"), dispatched[1].Upload);
    }

    [TestMethod]
    public async Task RunAsync_UploadGivenAfterTheUrls_StillPairsInOrder()
    {
        await RunAsync("http://h/1/", "-T", "a", "http://h/2/", "-T", "b");

        Diagnostics.Assert("first URL", "http://h/1/a", dispatched[0].Url);
        Assert.AreEqual("http://h/1/a", dispatched[0].Url);
        Diagnostics.Assert("second URL", "http://h/2/b", dispatched[1].Url);
        Assert.AreEqual("http://h/2/b", dispatched[1].Url);
    }

    [TestMethod]
    public async Task RunAsync_MoreUrlsThanUploads_SendsTheLaterUrlUnchangedWithNoUpload()
    {
        await RunAsync("-T", "a", "http://h/1/", "http://h/2/");

        Diagnostics.Assert("second URL", "http://h/2/", dispatched[1].Url);
        Assert.AreEqual("http://h/2/", dispatched[1].Url);
        Diagnostics.Assert("second upload is null", true, dispatched[1].Upload is null);
        Assert.IsNull(dispatched[1].Upload);
    }

    [TestMethod]
    [DataRow("-")]
    [DataRow(".")]
    public async Task RunAsync_UploadFromStandardInput_SendsStandardInputToTheUrlUnchanged(string uploadFile)
    {
        int exitCode = await RunAsync("-T", uploadFile, "http://h/d/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("dispatched URL", "http://h/d/", dispatched.Single().Url);
        Assert.AreEqual("http://h/d/", dispatched.Single().Url);
        Diagnostics.Diff("upload", new byte[] { 1, 2, 3 }, dispatched.Single().Upload);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, dispatched.Single().Upload);
        Diagnostics.Assert("read paths", string.Empty, string.Join(", ", files.ReadPaths));
        Assert.IsEmpty(files.ReadPaths);
    }

    [TestMethod]
    public async Task RunAsync_EmptyUpload_SendsTheUrlUnchangedWithNoUpload()
    {
        await RunAsync("-T", string.Empty, "-T", "a", "http://h/1/", "http://h/2/");

        Diagnostics.Assert("first URL", "http://h/1/", dispatched[0].Url);
        Assert.AreEqual("http://h/1/", dispatched[0].Url);
        Diagnostics.Assert("first upload is null", true, dispatched[0].Upload is null);
        Assert.IsNull(dispatched[0].Upload);
        Diagnostics.Assert("second URL", "http://h/2/a", dispatched[1].Url);
        Assert.AreEqual("http://h/2/a", dispatched[1].Url);
    }

    [TestMethod]
    public async Task RunAsync_UploadToMalformedUrl_Exits3WithoutOpeningTheFile()
    {
        int exitCode = await RunAsync("-T", "nosuchfile", "http://h/d ir/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.UrlMalformat, exitCode);
        Assert.AreEqual((int)CurlExitCode.UrlMalformat, exitCode);
        Diagnostics.Assert("read paths", string.Empty, string.Join(", ", files.ReadPaths));
        Assert.IsEmpty(files.ReadPaths);
        Diagnostics.Assert("dispatched count", 0, dispatched.Count);
        Assert.IsEmpty(dispatched);
        Diagnostics.Diff(
            "stderr",
            "curl: (3) URL using bad/illegal format or missing URL\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            $"curl: (3) URL using bad/illegal format or missing URL{NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadToMalformedUrl_PrintsNoWarningLinesAndAnEmptyEffectiveUrl()
    {
        int exitCode = await RunWithWarningLinesAsync("-s", "-T", "a", "-w", "[%{url_effective}]", "http://h/d ir/");

        Diagnostics.Assert("exit code", 3, exitCode);
        Assert.AreEqual(3, exitCode);
        Diagnostics.Diff("stderr", string.Empty, Normalized(StandardErrorText));
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.Diff("stdout", "[]", Normalized(StandardOutputText));
        Assert.AreEqual("[]", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadFileThatCannotBeOpened_Exits26AndStopsTheRun()
    {
        int exitCode = await RunAsync("-T", "nosuchfile", "-T", "a", "http://h/d/", "http://h/2/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.ReadError, exitCode);
        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Diagnostics.Assert("dispatched count", 0, dispatched.Count);
        Assert.IsEmpty(dispatched);
        Diagnostics.Diff(
            "stderr",
            "curl: cannot open 'nosuchfile'\n"
            + CommandLineRefusal.TryHelpLine + "\n"
            + $"curl: (26) {MultipartFormBodyBuilder.OpenFailedMessage}\n",
            Normalized(StandardErrorText));
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

        Diagnostics.Assert("exit code", 26, exitCode);
        Assert.AreEqual(26, exitCode);
        Diagnostics.Diff(
            "stderr",
            "curl: cannot open 'nosuchfile'\n" + CommandLineRefusal.TryHelpLine + "\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: cannot open 'nosuchfile'" + NewLine + CommandLineRefusal.TryHelpLine + NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.Diff("stdout", "http://h/d/nosuchfile", Normalized(StandardOutputText));
        Assert.AreEqual("http://h/d/nosuchfile", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadFileThatCannotBeOpened_PrintsTheWarningLinesFirst()
    {
        await RunWithWarningLinesAsync("-T", "nosuchfile", "http://h/d/");

        Diagnostics.Assert(
            "stderr starts with the warning then the cannot-open line",
            true,
            StandardErrorText.StartsWith("Warning: w" + NewLine + "curl: cannot open 'nosuchfile'", StringComparison.Ordinal));
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

        Diagnostics.Assert("dispatched URL", expected, dispatched.Single().Url);
        Assert.AreEqual(expected, dispatched.Single().Url);
        Diagnostics.Diff("stdout", expected, Normalized(StandardOutputText));
        Assert.AreEqual(expected, Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadWithOutputFile_SendsTheFileAndWritesTheOutputFile()
    {
        int exitCode = await RunAsync("-T", "a", "-o", "out", "http://h/1/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("upload", Encoding.ASCII.GetBytes("A"), dispatched.Single().Upload);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("A"), dispatched.Single().Upload);
        Diagnostics.Assert("out was written", true, files.Written.ContainsKey("out"));
        Assert.IsTrue(files.Written.ContainsKey("out"));
    }

    [TestMethod]
    public async Task RunAsync_UploadWithForm_WarnsSendsNothingAndExitsTwo()
    {
        int exitCode = await RunAsync("-T", "a", "-F", "x=y", "http://h/1/");

        string expected = "Warning: You can only select one HTTP request method! You asked for both PUT " + NewLine
            + "Warning: (-T, --upload-file) and multipart formpost (-F, --form)." + NewLine;
        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert("dispatched count", 0, dispatched.Count);
        Diagnostics.Diff("stderr", expected, StandardErrorText);
        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(dispatched);
        Assert.AreEqual(expected, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_DataWithUpload_WarnsSendsNothingAndExitsTwo()
    {
        int exitCode = await RunAsync("-d", "a=1", "-T", "a", "http://h/1/");

        string expected = "Warning: You can only select one HTTP request method! You asked for both PUT " + NewLine
            + "Warning: (-T, --upload-file) and POST (-d, --data)." + NewLine;
        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert("dispatched count", 0, dispatched.Count);
        Diagnostics.Diff("stderr", expected, StandardErrorText);
        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(dispatched);
        Assert.AreEqual(expected, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_SilentDataWithUpload_WritesNothingAndExitsTwo()
    {
        int exitCode = await RunAsync("-s", "-d", "a=1", "-T", "a", "http://h/1/");

        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert("dispatched count", 0, dispatched.Count);
        Diagnostics.Assert("stderr", string.Empty, StandardErrorText);
        Assert.AreEqual(2, exitCode);
        Assert.IsEmpty(dispatched);
        Assert.IsEmpty(StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_EmptyUploadThenUploadWithData_PostsTheFirstUrlThenRefusesTheSecond()
    {
        int exitCode = await RunAsync("-T", string.Empty, "-T", "a", "-d", "a=1", "http://h/1/", "http://h/2/");

        string expected = "Warning: You can only select one HTTP request method! You asked for both PUT " + NewLine
            + "Warning: (-T, --upload-file) and POST (-d, --data)." + NewLine;
        Diagnostics.Assert("exit code", 2, exitCode);
        Diagnostics.Assert("dispatched count", 1, dispatched.Count);
        Diagnostics.Diff("stderr", expected, StandardErrorText);
        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("http://h/1/", dispatched.Single().Url);
        Assert.AreEqual(expected, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_UploadWithHeaderFile_SendsTheFile()
    {
        await RunAsync("-T", "a", "-D", "headers", "http://h/1/");

        Diagnostics.Diff("upload", Encoding.ASCII.GetBytes("A"), dispatched.Single().Upload);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("A"), dispatched.Single().Upload);
    }

    [TestMethod]
    public async Task RunAsync_UploadGlob_UploadsEachMatchToItsOwnUrlInOrder()
    {
        int exitCode = await RunAsync("-T", "{local.txt,sub/in.txt}", "http://h/g/");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Assert("dispatched count", 2, dispatched.Count);
        Assert.HasCount(2, dispatched);
        Diagnostics.Assert("first URL", "http://h/g/local.txt", dispatched[0].Url);
        Assert.AreEqual("http://h/g/local.txt", dispatched[0].Url);
        Diagnostics.Diff("first upload", Encoding.ASCII.GetBytes("local"), dispatched[0].Upload);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("local"), dispatched[0].Upload);
        Diagnostics.Assert("second URL", "http://h/g/in.txt", dispatched[1].Url);
        Assert.AreEqual("http://h/g/in.txt", dispatched[1].Url);
        Diagnostics.Diff("second upload", Encoding.ASCII.GetBytes("in"), dispatched[1].Upload);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("in"), dispatched[1].Upload);
    }

    [TestMethod]
    public async Task RunAsync_UploadGlobWithTwoUrlsAndTwoOutputs_WritesBothOutputFilesAndNothingToStdout()
    {
        // upstream test2013, measured on curl 8.21.0 on 2026-10-08 (BL-1812 Notes): the glob's
        // transfers share the first URL's --output, the second URL takes the second.
        files.ExistingContent["upload1"] = Encoding.ASCII.GetBytes("first!\n");
        files.ExistingContent["upload2"] = Encoding.ASCII.GetBytes("second\n");

        int exitCode = await RunAsync(
            "-T", "upload{1,2}", "http://h/2013", "http://h/20130002", "--silent", "--output=first-out", "--output=second-out");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff(
            "dispatched URLs",
            "http://h/2013\nhttp://h/2013\nhttp://h/20130002",
            string.Join("\n", dispatched.Select(transfer => transfer.Url)));
        CollectionAssert.AreEqual(
            new[] { "http://h/2013", "http://h/2013", "http://h/20130002" },
            dispatched.Select(transfer => transfer.Url).ToArray());
        Diagnostics.Assert("third upload is null", true, dispatched[2].Upload is null);
        Assert.IsNull(dispatched[2].Upload);
        Diagnostics.Diff("written files", "first-out, second-out", string.Join(", ", files.Written.Keys.Order(StringComparer.Ordinal)));
        CollectionAssert.AreEqual(new[] { "first-out", "second-out" }, files.Written.Keys.Order(StringComparer.Ordinal).ToArray());
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_NamedUploadGlobWithOutputNamedByIt_WritesOneOutputFilePerUploadAndNothingToStdout()
    {
        // upstream test2014, measured on curl 8.21.0 on 2026-10-08 (BL-1812 Notes).
        files.ExistingContent["upload1"] = Encoding.ASCII.GetBytes("first!\n");
        files.ExistingContent["upload2"] = Encoding.ASCII.GetBytes("second\n");

        int exitCode = await RunAsync("-T", "upload{<hej>1,2}", "http://h/2014", "--silent", "--output=out-#<hej>");

        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        Diagnostics.Diff("written files", "out-1, out-2", string.Join(", ", files.Written.Keys.Order(StringComparer.Ordinal)));
        CollectionAssert.AreEqual(new[] { "out-1", "out-2" }, files.Written.Keys.Order(StringComparer.Ordinal).ToArray());
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_UploadGlobWithUrlGlob_TakesTheUploadFilesAsTheOuterLoop()
    {
        await RunAsync("-T", "{local.txt,sub/in.txt}", "http://h/{x,y}/");

        Diagnostics.Diff(
            "dispatched URLs",
            "http://h/x/local.txt\nhttp://h/y/local.txt\nhttp://h/x/in.txt\nhttp://h/y/in.txt",
            string.Join("\n", dispatched.Select(transfer => transfer.Url)));
        CollectionAssert.AreEqual(
            new[] { "http://h/x/local.txt", "http://h/y/local.txt", "http://h/x/in.txt", "http://h/y/in.txt" },
            dispatched.Select(transfer => transfer.Url).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_MalformedUploadGlob_Exits3WithTheGlobMessageAboutTheUploadText()
    {
        int exitCode = await RunAsync("-T", "f[3-1].txt", "http://h/g/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.UrlMalformat, exitCode);
        Assert.AreEqual((int)CurlExitCode.UrlMalformat, exitCode);
        Diagnostics.Assert("dispatched count", 0, dispatched.Count);
        Assert.IsEmpty(dispatched);
        Diagnostics.Assert("read paths", string.Empty, string.Join(", ", files.ReadPaths));
        Assert.IsEmpty(files.ReadPaths);
        Diagnostics.Diff(
            "stderr",
            "curl: (3) bad range in position 7:\nf[3-1].txt\n      ^\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: (3) bad range in position 7:" + NewLine + "f[3-1].txt" + NewLine + "      ^" + NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_GlobOffUploadGlob_TriesTheOneLiteralFile()
    {
        files.UnreadablePaths.Add("{local.txt,sub/in.txt}");
        Diagnostics.Arrange("unreadable paths added", "{local.txt,sub/in.txt}");

        int exitCode = await RunAsync("-g", "-T", "{local.txt,sub/in.txt}", "http://h/g/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.ReadError, exitCode);
        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Diagnostics.Assert("dispatched count", 0, dispatched.Count);
        Assert.IsEmpty(dispatched);
        Diagnostics.Assert("read paths", "{local.txt,sub/in.txt}", string.Join(", ", files.ReadPaths));
        CollectionAssert.AreEqual(new[] { "{local.txt,sub/in.txt}" }, files.ReadPaths.ToArray());
        Diagnostics.Assert(
            "stderr starts with the cannot-open line",
            true,
            StandardErrorText.StartsWith("curl: cannot open '{local.txt,sub/in.txt}'" + NewLine, StringComparison.Ordinal));
        StringAssert.StartsWith(
            Encoding.UTF8.GetString(standardError.ToArray()),
            "curl: cannot open '{local.txt,sub/in.txt}'" + NewLine);
    }

    [TestMethod]
    [DataRow("http://h/g/")]
    [DataRow("http://down/g/")]
    public async Task RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch(string url)
    {
        int exitCode = await RunAsync("-T", "{nosuchfile,local.txt}", "-w", "%{url_effective} %{exitcode}\\n", url);

        Diagnostics.Assert("exit code", (int)CurlExitCode.ReadError, exitCode);
        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Diagnostics.Assert("dispatched count", 0, dispatched.Count);
        Assert.IsEmpty(dispatched);
        Diagnostics.Diff(
            "stderr",
            "curl: cannot open 'nosuchfile'\n"
            + CommandLineRefusal.TryHelpLine + "\n"
            + $"curl: (26) {MultipartFormBodyBuilder.OpenFailedMessage}\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: cannot open 'nosuchfile'" + NewLine
            + CommandLineRefusal.TryHelpLine + NewLine
            + $"curl: (26) {MultipartFormBodyBuilder.OpenFailedMessage}{NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.Diff("stdout", $"{url}nosuchfile 26\n", Normalized(StandardOutputText));
        Assert.AreEqual($"{url}nosuchfile 26\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun()
    {
        int exitCode = await RunAsync(
            "-T", "{local.txt,nosuchfile,sub/in.txt}", "-w", "%{url_effective} %{exitcode}\\n", "http://h/g/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.ReadError, exitCode);
        Assert.AreEqual((int)CurlExitCode.ReadError, exitCode);
        Diagnostics.Assert("dispatched URL", "http://h/g/local.txt", dispatched.Single().Url);
        Assert.AreEqual("http://h/g/local.txt", dispatched.Single().Url);
        Diagnostics.Diff(
            "stderr",
            "curl: cannot open 'nosuchfile'\n"
            + CommandLineRefusal.TryHelpLine + "\n"
            + $"curl: (26) {MultipartFormBodyBuilder.OpenFailedMessage}\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: cannot open 'nosuchfile'" + NewLine
            + CommandLineRefusal.TryHelpLine + NewLine
            + $"curl: (26) {MultipartFormBodyBuilder.OpenFailedMessage}{NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.Diff(
            "stdout",
            "http://h/g/local.txt 0\nhttp://h/g/nosuchfile 26\n",
            Normalized(StandardOutputText));
        Assert.AreEqual(
            "http://h/g/local.txt 0\nhttp://h/g/nosuchfile 26\n",
            Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterAFailure_ReportsThePreviousCodeWithCurlsText()
    {
        int exitCode = await RunAsync(
            "-T", "{local.txt,nosuchfile,sub/in.txt}", "-w", "%{url_effective} %{exitcode} %{errormsg}\\n", "http://down/g/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntConnect, exitCode);
        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        Diagnostics.Assert("dispatched URL", "http://down/g/local.txt", dispatched.Single().Url);
        Assert.AreEqual("http://down/g/local.txt", dispatched.Single().Url);
        Diagnostics.Diff(
            "stderr",
            $"curl: (7) {DownMessage}\n"
            + "curl: cannot open 'nosuchfile'\n"
            + CommandLineRefusal.TryHelpLine + "\n"
            + "curl: (7) Could not connect to server\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            $"curl: (7) {DownMessage}{NewLine}"
            + "curl: cannot open 'nosuchfile'" + NewLine
            + CommandLineRefusal.TryHelpLine + NewLine
            + $"curl: (7) Could not connect to server{NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
        Diagnostics.Diff(
            "stdout",
            $"http://down/g/local.txt 7 {DownMessage}\nhttp://down/g/nosuchfile 7 Could not connect to server\n",
            Normalized(StandardOutputText));
        Assert.AreEqual(
            $"http://down/g/local.txt 7 {DownMessage}\nhttp://down/g/nosuchfile 7 Could not connect to server\n",
            Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_UploadFileThatCannotBeOpenedAfterAFailedUrl_ReportsThePreviousCode()
    {
        int exitCode = await RunAsync("-s", "-T", "local.txt", "-T", "nosuchfile", "http://down/a/", "http://h/b/", "http://h/c/");

        Diagnostics.Assert("exit code", (int)CurlExitCode.CouldntConnect, exitCode);
        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        Diagnostics.Assert("dispatched URL", "http://down/a/local.txt", dispatched.Single().Url);
        Assert.AreEqual("http://down/a/local.txt", dispatched.Single().Url);
        Diagnostics.Diff(
            "stderr",
            "curl: cannot open 'nosuchfile'\n" + CommandLineRefusal.TryHelpLine + "\n",
            Normalized(StandardErrorText));
        Assert.AreEqual(
            "curl: cannot open 'nosuchfile'" + NewLine + CommandLineRefusal.TryHelpLine + NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    private const string DownMessage = "Failed to connect to down port 80 after 0 ms: Could not connect to server";

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
            return context.Url.Host == "down"
                ? TransferResult.Failure(CurlExitCode.CouldntConnect, DownMessage)
                : TransferResult.Success(0);
        });

    private static string UrlText(CurlUrl url) =>
        $"{url.Scheme}://{url.Host}{url.AbsolutePath}";

    private static string Normalized(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private Task<int> RunAsync(params string[] arguments)
    {
        Diagnostics.Arrange("warning lines before each transfer", "(none)");

        return RunWithDiagnosticsAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([CreateHandler()])),
                files,
                files,
                standardOutput,
                standardError,
                standardInput,
                runsOnWindows: false,
                formBodyBuilder: new MultipartFormBodyBuilder(files, Encoding.UTF8, () => "b")),
            arguments);
    }

    private Task<int> RunWithWarningLinesAsync(params string[] arguments)
    {
        Diagnostics.Arrange("warning lines before each transfer", "Warning: w");

        return RunWithDiagnosticsAsync(
            new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([CreateHandler()]), ["Warning: w"]),
                files,
                files,
                standardOutput,
                standardError,
                standardInput,
                runsOnWindows: false),
            arguments);
    }

    private async Task<int> RunWithDiagnosticsAsync(CurlCommandRunner runner, string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange(
            "handler behaviour",
            "http records each URL and upload; host down fails with exit 7, any other host succeeds with 0 bytes");
        Diagnostics.Arrange("existing files", string.Join(", ", files.ExistingContent.Keys.Order(StringComparer.Ordinal)));
        Diagnostics.Arrange("unreadable paths", string.Join(", ", files.UnreadablePaths.Order(StringComparer.Ordinal)));
        Diagnostics.Bytes("stdin", standardInput.ToArray());

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Act("stderr", Normalized(StandardErrorText));
        Diagnostics.Act("dispatched URLs", string.Join(", ", dispatched.Select(transfer => transfer.Url)));
        return exitCode;
    }
}
