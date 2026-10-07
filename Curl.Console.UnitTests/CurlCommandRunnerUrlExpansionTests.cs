using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins what happens to each command-line URL before it is dispatched: its glob is expanded
/// (unless <c>-g</c>), each <c>#N</c> in its <c>-o</c> name substituted, an <c>ipfs://</c> or
/// <c>ipns://</c> URL rewritten to its gateway URL, and a URL typed without a scheme given the
/// one curl guesses. Every expectation was measured against curl 8.21.0 (mingw, Schannel) on
/// 2026-09-27; the commands are in BL-240's Notes. Every test runs over fake handlers.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerUrlExpansionTests
{
    private static readonly string NewLine = Environment.NewLine;

    private static readonly string TryHelpLine = "curl: try 'curl --help' or 'curl --manual' for more information" + NewLine;

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly InMemoryDataFileReader dataFiles = new();
    private readonly Dictionary<string, string> environment = [];
    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    private string StandardOutputText => Encoding.ASCII.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_UrlWithoutScheme_IsTransferredAsHttpAndPrintedAsTyped()
    {
        int exitCode = await RunAsync(["-w", "|%{url}|%{url_effective}|%{scheme}|%{urlnum}", "localhost:1/a"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("http://localhost:1/a", Assert.ContainsSingle(http.Contexts).Url.OriginalString);
        Assert.AreEqual("/a|localhost:1/a|http://localhost:1/a|http|0", StandardOutputText);
    }

    [TestMethod]
    [DataRow("localhost:1")]
    [DataRow("http://localhost:1")]
    public async Task RunAsync_UrlWithoutPath_PrintsTheEffectiveUrlWithTheRootPath(string url)
    {
        int exitCode = await RunAsync(["-w", "|%{url}|%{url_effective}", url]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/|" + url + "|http://localhost:1/", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UrlWithoutPathButWithAQuery_PrintsTheRootPathBeforeTheQuery()
    {
        int exitCode = await RunAsync(["-w", "|%{url_effective}", "localhost:1?q"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/|http://localhost:1/?q", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UrlWithoutSchemeWhoseHostStartsWithFtp_IsTransferredAsFtp()
    {
        RecordingProtocolHandler ftp = RecordingProtocolHandler.WritingPath("ftp");

        int exitCode = await RunAsync(["-w", "|%{url}|%{url_effective}|%{scheme}", "ftp.localhost:1/x"], ftp);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ftp", Assert.ContainsSingle(ftp.Contexts).Url.Scheme);
        Assert.AreEqual("/x|ftp.localhost:1/x|ftp://ftp.localhost:1/x|ftp", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_RangeGlobWithHashInOutputName_TransfersEachUrlToItsOwnFile()
    {
        int exitCode = await RunAsync(
            ["-o", "#1.txt", "-w", "%{url}|%{urlnum}|%{xfer_id}|%{filename_effective}\\n", "http://h/[1-3]"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/1", WrittenText("1.txt"));
        Assert.AreEqual("/2", WrittenText("2.txt"));
        Assert.AreEqual("/3", WrittenText("3.txt"));
        Assert.AreEqual(
            "http://h/1|0|0|1.txt\nhttp://h/2|0|1|2.txt\nhttp://h/3|0|2|3.txt\n",
            StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_GlobThenPlainUrl_CountsUrlsAndTransfersApart()
    {
        int exitCode = await RunAsync(
            ["-w", "%{urlnum}|%{xfer_id}|%{filename_effective}\\n", "-o", "o#1", "http://h/{a,b}.txt", "http://h/a.txt", "-o", "last"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/a.txt", WrittenText("oa"));
        Assert.AreEqual("/b.txt", WrittenText("ob"));
        Assert.AreEqual("/a.txt", WrittenText("last"));
        Assert.AreEqual("0|0|oa\n0|1|ob\n1|2|last\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_GlobWithRemoteName_SavesEachUrlUnderItsOwnName()
    {
        int exitCode = await RunAsync(["-O", "http://h/{a,b}.txt"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/a.txt", WrittenText("a.txt"));
        Assert.AreEqual("/b.txt", WrittenText("b.txt"));
    }

    [TestMethod]
    public async Task RunAsync_FailedGlobUrl_GoesOnToTheNextUrlOfTheGlob()
    {
        RecordingProtocolHandler failing = new(
            "http",
            context => ValueTask.FromResult(context.Url.AbsolutePath == "/nope"
                ? TransferResult.Failure(CurlExitCode.CouldntConnect, "refused")
                : TransferResult.Success(0)));

        int exitCode = await RunAsync(["-sS", "-w", "%{url}|%{exitcode}\\n", "http://h/{nope,a}"], failing);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("curl: (7) refused" + NewLine, StandardErrorText);
        Assert.AreEqual("http://h/nope|7\nhttp://h/a|0\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_BadGlob_PrintsCurlsPositionLinesAndStopsTheRunWithExit3()
    {
        int exitCode = await RunAsync(["-sS", "-w", "%{url}\\n", "http://127.0.0.1:1/[3-1]", "http://127.0.0.1:1/y"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(
            "curl: (3) bad range in position 25:" + NewLine
            + "http://127.0.0.1:1/[3-1]" + NewLine
            + new string(' ', 24) + "^" + NewLine,
            StandardErrorText);
        Assert.IsEmpty(http.Contexts);
        Assert.AreEqual(string.Empty, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_BadGlobAfterAGoodUrl_TransfersTheFirstAndReturns3()
    {
        int exitCode = await RunAsync(["-sS", "-w", "%{url}|%{exitcode}\\n", "http://127.0.0.1:1/y", "http://127.0.0.1:1/[3-1]"]);

        Assert.AreEqual(3, exitCode);
        Assert.ContainsSingle(http.Contexts);
        Assert.AreEqual("/yhttp://127.0.0.1:1/y|0\n", StandardOutputText);
        Assert.StartsWith("curl: (3) bad range in position 25:", StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_BadGlobUnderSilent_PrintsNothing()
    {
        int exitCode = await RunAsync(["-s", "http://127.0.0.1:1/[3-1]"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(string.Empty, StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_GlobOff_TransfersTheUrlOnceAndOpensTheOutputNameAsWritten()
    {
        int exitCode = await RunAsync(["-g", "-o", "q?x#1", "http://h/a"], runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.ContainsSingle(http.Contexts);
        Assert.AreEqual("/a", WrittenText("q?x#1"));
    }

    [TestMethod]
    public async Task RunAsync_OutputNameReferencesANamedGlob_SavesEachUrlUnderItsValue()
    {
        int exitCode = await RunAsync(["http://h/hello[<test>7-8]", "-o", "dump-#<test>"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/hello7", WrittenText("dump-7"));
        Assert.AreEqual("/hello8", WrittenText("dump-8"));
    }

    [TestMethod]
    public async Task RunAsync_OutputNameReferencesNoGlobWithThatName_Exits43BeforeAnyTransfer()
    {
        int exitCode = await RunAsync(["http://h/{<test>A,B}{<moo>C,D}", "-o", "somewhere/#<foo>"]);

        Assert.AreEqual((int)CurlExitCode.BadFunctionArgument, exitCode);
        Assert.IsEmpty(http.Contexts);
        Assert.IsEmpty(fileSystem.Written);
        Assert.AreEqual(
            "curl: (43) no glob exists with this name in position 16:" + NewLine
                + "somewhere/#<foo>" + NewLine
                + "               ^" + NewLine,
            StandardErrorText);
    }

    [TestMethod]
    public async Task RunAsync_OutputNameReferencesAnUploadGlobName_SavesEachUploadUnderItsValue()
    {
        fileSystem.ExistingContent["a"] = Encoding.ASCII.GetBytes("A");
        fileSystem.ExistingContent["b"] = Encoding.ASCII.GetBytes("B");

        int exitCode = await RunAsync(["-T", "{<f>a,b}", "-o", "up-#<f>", "http://h/g/"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/g/a", WrittenText("up-a"));
        Assert.AreEqual("/g/b", WrittenText("up-b"));
    }

    [TestMethod]
    public async Task RunAsync_GlobbingOnWindows_SanitizesTheOutputName()
    {
        int exitCode = await RunAsync(["-o", "q?y", "http://h/a"], runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/a", WrittenText("q_y"));
    }

    [TestMethod]
    public async Task RunAsync_DumpHeaderFileForAGlob_TruncatesForTheFirstUrlAndAppendsTheSecond()
    {
        int exitCode = await RunAsync(["-D", "hd.txt", "http://h/{a,b}"]);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { FileWriteMode.Truncate, FileWriteMode.Append }, fileSystem.WriteModes);
    }

    [TestMethod]
    public async Task RunAsync_IpfsGlobWithGatewayOption_FetchesEachGatewayUrl()
    {
        int exitCode = await RunAsync(
            ["--ipfs-gateway", "http://127.0.0.1:1", "-w", "|%{url}|%{url_effective}|%{urlnum}\\n", "ipfs://bafy{a,b}/x"]);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[] { "http://127.0.0.1:1/ipfs/bafya/x", "http://127.0.0.1:1/ipfs/bafyb/x" },
            http.Contexts.Select(context => context.Url.OriginalString).ToArray());
        Assert.AreEqual(
            "/ipfs/bafya/x|http://127.0.0.1:1/ipfs/bafya/x|http://127.0.0.1:1/ipfs/bafya/x|0\n"
            + "/ipfs/bafyb/x|http://127.0.0.1:1/ipfs/bafyb/x|http://127.0.0.1:1/ipfs/bafyb/x|0\n",
            StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IpfsGatewayOptionWithoutScheme_IsReadAsHttp()
    {
        int exitCode = await RunAsync(["--ipfs-gateway", "127.0.0.1:1", "-w", "|%{url}", "ipfs://bafyabc"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("/ipfs/bafyabc|http://127.0.0.1:1/ipfs/bafyabc", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IpfsGatewayFromTheEnvironment_IsUsed()
    {
        environment[IpfsGatewayRewriter.GatewayVariableName] = "http://gw:8080";

        int exitCode = await RunAsync(["ipns://name/p"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("http://gw:8080/ipns/name/p", Assert.ContainsSingle(http.Contexts).Url.OriginalString);
    }

    [TestMethod]
    public async Task RunAsync_IpfsGatewayFromTheHomeGatewayFile_IsReadThroughTheDataFileReader()
    {
        environment[IpfsGatewayRewriter.HomeVariableName] = "/home/u";
        dataFiles.Files["/home/u/.ipfs/gateway"] = Encoding.UTF8.GetBytes("http://gw:1\nignored\n");

        int exitCode = await RunAsync(["ipfs://cid"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("http://gw:1/ipfs/cid", Assert.ContainsSingle(http.Contexts).Url.OriginalString);
    }

    [TestMethod]
    public async Task RunAsync_IpfsWithNoGatewayAnywhere_PrintsCurlsLinesEvenSilentAndStopsTheRunWithExit37()
    {
        environment[IpfsGatewayRewriter.HomeVariableName] = "/home/u";

        int exitCode = await RunAsync(
            ["-s", "-o", "x.out", "-w", "[%{errormsg}|%{url}|%{url_effective}|%{urlnum}|%{xfer_id}|%{conn_id}|%{scheme}|%{filename_effective}|%{exitcode}]\\n", "ipfs://bafyabc", "http://h/z"]);

        Assert.AreEqual(37, exitCode);
        Assert.AreEqual("curl: IPFS automatic gateway detection failed" + NewLine + TryHelpLine, StandardErrorText);
        Assert.AreEqual("[Could not read a file:// file|ipfs://bafyabc||0|-1|-1||x.out|37]\n", StandardOutputText);
        Assert.IsEmpty(http.Contexts);
        CollectionAssert.AreEqual(new[] { "/home/u/.ipfs/gateway" }, dataFiles.PathsRead);
    }

    [TestMethod]
    public async Task RunAsync_IpfsWithARunnerGivenNoEnvironment_ReadsNoVariableAndNoFile()
    {
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                configFileReader: dataFiles)
            .RunAsync(["ipfs://bafyabc"]);

        Assert.AreEqual(37, exitCode);
        Assert.IsEmpty(dataFiles.PathsRead);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    public async Task RunAsync_IpfsFailureWithOutputDirectory_PrintsTheSubstitutedNameUnderIt()
    {
        int exitCode = await RunAsync(
            ["-s", "-w", "[%{filename_effective}]\\n", "--output-dir", "sub", "-o", "x#1?.out", "ipfs://bafy{a,b}"],
            runsOnWindows: true);

        Assert.AreEqual(37, exitCode);
        Assert.AreEqual("[sub/xa_.out]\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IpfsFailureWithoutOutputFileOnWindows_WritesTheWriteOutLineFeedUnchanged()
    {
        int exitCode = await RunAsync(["-s", "-w", "[x]\\n", "ipfs://bafyabc"], runsOnWindows: true);

        Assert.AreEqual(37, exitCode);
        Assert.AreEqual("[x]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IpfsWithMalformedGateway_PrintsCurlsLinesAndReturns3()
    {
        int exitCode = await RunAsync(
            ["-sS", "--ipfs-gateway", "http://h:1/?q", "-w", "[%{url}|%{url_effective}|%{errormsg}|%{exitcode}]\\n", "ipfs://bafyabc"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("curl: malformed target URL" + NewLine + TryHelpLine, StandardErrorText);
        Assert.AreEqual("[ipfs://bafyabc||URL using bad/illegal format or missing URL|3]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IpfsWithMalformedGatewayOption_PrintsCurlsLinesEvenSilentAndReturns43()
    {
        int exitCode = await RunAsync(
            ["-s", "-w", "[%{errormsg}] [%{exitcode}]\\n", "--ipfs-gateway", ":::", "ipfs://cid/x"]);

        Assert.AreEqual(43, exitCode);
        Assert.AreEqual("curl: --ipfs-gateway was given a malformed URL" + NewLine + TryHelpLine, StandardErrorText);
        Assert.AreEqual("[A libcurl function was given a bad argument] [43]\n", StandardOutputText);
    }

    [TestMethod]
    [DataRow("-O", "ipfs://bafyabc/n.txt")]
    [DataRow("-O", "ipfs://bafyabc")]
    [DataRow("-O", "ipns://k51/n.txt")]
    [DataRow("--remote-name-all", "ipfs://bafyabc/n.txt")]
    public async Task RunAsync_RemoteNameOnIpfsUrl_PrintsCurlsLinesAndReturns1BeforeAnyGatewayIsLookedUp(
        string remoteNameOption,
        string url)
    {
        environment[IpfsGatewayRewriter.HomeVariableName] = "/home/u";

        int exitCode = await RunAsync(
            [remoteNameOption, url, "-w", "[%{filename_effective}|%{exitcode}|%{errormsg}|%{url}|%{url_effective}|%{xfer_id}|%{conn_id}|%{http_code}]\\n"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(
            "curl: Failed to extract a filename from the URL to use for storage" + NewLine
            + "curl: (1) Unsupported protocol" + NewLine,
            StandardErrorText);
        Assert.AreEqual("[|1|Unsupported protocol|" + url + "||-1|-1|000]\n", StandardOutputText);
        Assert.IsEmpty(dataFiles.PathsRead);
        Assert.IsEmpty(http.Contexts);
        Assert.IsEmpty(fileSystem.Written);
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameOnIpfsUrlWithAGateway_IsRefusedTheSameWay()
    {
        int exitCode = await RunAsync(
            ["--ipfs-gateway", "http://127.0.0.1:9/", "-O", "ipfs://bafyabc/n.txt", "-w", "[%{url}|%{url_effective}|%{exitcode}]\\n"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(
            "curl: Failed to extract a filename from the URL to use for storage" + NewLine
            + "curl: (1) Unsupported protocol" + NewLine,
            StandardErrorText);
        Assert.AreEqual("[ipfs://bafyabc/n.txt||1]\n", StandardOutputText);
        Assert.IsEmpty(http.Contexts);
    }

    [TestMethod]
    [DataRow("-s", "")]
    [DataRow("-sS", "curl: Failed to extract a filename from the URL to use for storage\ncurl: (1) Unsupported protocol\n")]
    public async Task RunAsync_RemoteNameOnIpfsUrlUnderSilent_PrintsTheLinesOnlyWithShowError(
        string silentOption,
        string expectedStandardError)
    {
        int exitCode = await RunAsync([silentOption, "-O", "ipfs://bafyabc/n.txt", "-w", "[%{exitcode}]"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(expectedStandardError.Replace("\n", NewLine, StringComparison.Ordinal), StandardErrorText);
        Assert.AreEqual("[1]", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_RemoteNameOnIpfsUrlBetweenTwoUrls_StopsTheRunAfterTheOneBeforeIt()
    {
        int exitCode = await RunAsync(
            ["-s", "-o", "y", "http://h/a", "-O", "ipfs://bafyabc/n.txt", "-o", "z", "http://h/b", "-w", "[%{filename_effective}|%{exitcode}|%{urlnum}|%{xfer_id}]\\n"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("http://h/a", Assert.ContainsSingle(http.Contexts).Url.OriginalString);
        Assert.AreEqual("[y|0|0|0]\n[|1|1|-1]\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_OutputFileOnIpfsUrl_IsNotRefusedAsARemoteName()
    {
        int exitCode = await RunAsync(["--ipfs-gateway", "http://gw:1", "-o", "x.out", "ipfs://bafyabc/n.txt"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("http://gw:1/ipfs/bafyabc/n.txt", Assert.ContainsSingle(http.Contexts).Url.OriginalString);
    }

    [TestMethod]
    public async Task RunAsync_IpfsUrlOnARunnerWithoutAnEnvironment_TakesTheGatewayOption()
    {
        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: fileSystem,
                configFileReader: dataFiles)
            .RunAsync(["--ipfs-gateway", "http://gw:1", "ipfs://bafyabc/n.txt", "ipfs://bafydef/m.txt"]);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[] { "http://gw:1/ipfs/bafyabc/n.txt", "http://gw:1/ipfs/bafydef/m.txt" },
            http.Contexts.Select(context => context.Url.OriginalString).ToArray());
    }

    private string WrittenText(string path) => Encoding.ASCII.GetString(fileSystem.Written[path].ToArray());

    private Task<int> RunAsync(IReadOnlyList<string> arguments, bool runsOnWindows = false) =>
        RunAsync(arguments, http, runsOnWindows);

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler, bool runsOnWindows = false) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows,
                outputPaths: fileSystem,
                configFileReader: dataFiles,
                readEnvironmentVariable: name => environment.GetValueOrDefault(name))
            .RunAsync(arguments);
}
