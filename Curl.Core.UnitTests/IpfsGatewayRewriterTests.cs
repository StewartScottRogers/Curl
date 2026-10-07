using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins the gateway URL <see cref="IpfsGatewayRewriter" /> gives an <c>ipfs</c> or <c>ipns</c>
/// URL, and its failures, each measured against curl 8.21.0 (mingw, Schannel) on 2026-09-27
/// as the <c>%{url_effective}</c>, stderr and exit code of
/// <c>curl -sS -m 1 -w '%{url_effective}\n' [--ipfs-gateway &lt;gateway&gt;] &lt;url&gt;</c>
/// with the gateway on an unreachable loopback port.
/// </summary>
[TestClass]
public sealed class IpfsGatewayRewriterTests
{
    private const string GatewayFile = "/home/u/.ipfs/gateway";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("http://127.0.0.1:1", "ipfs://bafyabc/x", "http://127.0.0.1:1/ipfs/bafyabc/x")]
    [DataRow("http://127.0.0.1:1/gw", "ipfs://bafyabc/x/y?q=1", "http://127.0.0.1:1/gw/ipfs/bafyabc/x/y?q=1")]
    [DataRow("http://127.0.0.1:1/gw/", "ipns://name.eth", "http://127.0.0.1:1/gw/ipns/name.eth")]
    [DataRow("http://127.0.0.1:1/", "ipns://name.eth/", "http://127.0.0.1:1/ipns/name.eth")]
    [DataRow("127.0.0.1:1", "ipfs://cid", "http://127.0.0.1:1/ipfs/cid")]
    [DataRow("http://127.0.0.1:1", "IPFS://CiD/A%20b", "http://127.0.0.1:1/ipfs/CiD/A%20b")]
    [DataRow("http://127.0.0.1:1", "ipfs://cid#frag", "http://127.0.0.1:1/ipfs/cid#frag")]
    [DataRow("http://u:p@127.0.0.1:1/p", "ipfs://cid", "http://127.0.0.1:1/p/ipfs/cid")]
    [DataRow("http://127.0.0.1:1", "ipfs://u:p@cid:99/x", "http://u:p@127.0.0.1:1/ipfs/cid/x")]
    [DataRow("http://127.0.0.1:1", "ipfs://u:@cid/x", "http://u:@127.0.0.1:1/ipfs/cid/x")]
    [DataRow("http://127.0.0.1:1", "ipfs://@cid/x", "http://@127.0.0.1:1/ipfs/cid/x")]
    [DataRow("ftp.127.0.0.1", "ipfs://bafyabc/x", "ftp://ftp.127.0.0.1:21/ipfs/bafyabc/x")]
    [DataRow("https://127.0.0.1", "ipfs://cid", "https://127.0.0.1:443/ipfs/cid")]
    [DataRow("ftp://127.0.0.1:1", "ipfs://cid", "ftp://127.0.0.1:1/ipfs/cid")]
    [DataRow("http://127.0.0.1", "ipfs://cid:99", "http://127.0.0.1:80/ipfs/cid")]
    [DataRow("HTTP://Host.Example", "ipfs://cid", "http://Host.Example:80/ipfs/cid")]
    [DataRow("http://127.0.0.1:1/a%20b", "ipfs://cid/c%2Fd", "http://127.0.0.1:1/a%20b/ipfs/cid/c/d")]
    [DataRow("http://127.0.0.1:1/a/b/", "ipfs://cid/", "http://127.0.0.1:1/a/b/ipfs/cid")]
    [DataRow("http://127.0.0.1:1/", "ipfs://cid//x", "http://127.0.0.1:1/ipfs/cid//x")]
    [DataRow("http://127.0.0.1:1/g#frag", "ipfs://cid/x#y", "http://127.0.0.1:1/g/ipfs/cid/x#y")]
    [DataRow("http://127.0.0.1:1/g?", "ipfs://cid/x#y", "http://127.0.0.1:1/g/ipfs/cid/x#y")]
    [DataRow("http://127.0.0.1:1/a/../b", "ipfs://cid/x#y", "http://127.0.0.1:1/b/ipfs/cid/x#y")]
    [DataRow("http://127.0.0.1:1/g%3Fx", "ipfs://cid/a%3Fb", "http://127.0.0.1:1/g%3Fx/ipfs/cid/a%3Fb")]
    [DataRow("http://127.0.0.1:1/g%3Fx", "ipfs://cid/a%7Eb%41", "http://127.0.0.1:1/g%3Fx/ipfs/cid/a~bA")]
    [DataRow("http://127.0.0.1:1/g%3Fx", "ipfs://c%41d/x", "http://127.0.0.1:1/g%3Fx/ipfs/cAd/x")]
    [DataRow("http://127.0.0.1:1/g%3Fx", "ipfs://cid/%C3%A9", "http://127.0.0.1:1/g%3Fx/ipfs/cid/%C3%A9")]
    [DataRow("http://127.0.0.1:1/g%3Fx", "ipfs://cid/a+b%2B!$&()*,;=:@[]", "http://127.0.0.1:1/g%3Fx/ipfs/cid/a+b+!$&()*,;=:@[]")]
    [DataRow("http://127.0.0.1:1/g%3Fx", "ipfs://cid/a/../b", "http://127.0.0.1:1/g%3Fx/ipfs/cid/b")]
    [DataRow("http://127.0.0.1:1/g%3Fx", "ipfs://cid/a%5Cb", "http://127.0.0.1:1/g%3Fx/ipfs/cid/a%5Cb")]
    [DataRow("http://127.0.0.1:1/", "ipfs://cid/a%7Fb", "http://127.0.0.1:1/ipfs/cid/a%7Fb")]
    [DataRow("http://127.0.0.1:1", "ipfs://cid/a%zzb%4", "http://127.0.0.1:1/ipfs/cid/a%25zzb%254")]
    [DataRow("http://127.0.0.1:1", "ipfs://cid/x?", "http://127.0.0.1:1/ipfs/cid/x")]
    [DataRow("http://127.0.0.1:1", "ipfs://cid/x#", "http://127.0.0.1:1/ipfs/cid/x")]
    [DataRow("http://127.0.0.1:1", "ipfs://cid?q", "http://127.0.0.1:1/ipfs/cid?q")]
    [DataRow("http://127.0.0.1:1", "ipfs://cid/x?a%20b&c", "http://127.0.0.1:1/ipfs/cid/x?a%20b&c")]
    public void TryRewrite_GatewayOption_BuildsTheMeasuredUrl(string gateway, string url, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter();
        diagnostics.Arrange("gateway option", gateway);
        diagnostics.Arrange("url", url);

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse(url), gateway, out string? gatewayUrl, out IpfsGatewayFailure? failure);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        diagnostics.Act("failure", failure?.Message);
        diagnostics.Assert("rewritten", true, rewritten);
        Assert.IsTrue(rewritten);
        diagnostics.Assert("gateway url", expected, gatewayUrl);
        Assert.AreEqual(expected, gatewayUrl);
        diagnostics.Assert("failure", null, failure?.Message);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void TryRewrite_EveryPrintableByte_EncodesTheMeasuredSet()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string path = string.Concat(Enumerable.Range(0x20, 0x7F - 0x20).Select(value => $"%{value:X2}"));
        const string Expected = "http://127.0.0.1:1/ipfs/cid/%20!%22%23$%25&'()*+,-./0123456789:;%3C=%3E%3F@ABCDEFGHIJKLMNOPQRSTUVWXYZ[%5C]%5E_%60abcdefghijklmnopqrstuvwxyz{%7C}~";
        diagnostics.Arrange("path", path);

        bool rewritten = CreateRewriter().TryRewrite(CurlUrl.Parse("ipfs://cid/" + path), "http://127.0.0.1:1/", out string? gatewayUrl, out _);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        Assert.IsTrue(rewritten);
        diagnostics.Assert("gateway url", Expected, gatewayUrl);
        Assert.AreEqual(
            Expected,
            gatewayUrl);
    }

    [TestMethod]
    public void TryRewrite_EveryHighByte_IsPercentEncoded()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string path = string.Concat(Enumerable.Range(0x80, 0x80).Select(value => $"%{value:X2}"));
        diagnostics.Arrange("path", path);

        bool rewritten = CreateRewriter().TryRewrite(CurlUrl.Parse("ipfs://cid/" + path), "http://127.0.0.1:1/", out string? gatewayUrl, out _);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        Assert.IsTrue(rewritten);
        diagnostics.Assert("gateway url", "http://127.0.0.1:1/ipfs/cid/" + path, gatewayUrl);
        Assert.AreEqual("http://127.0.0.1:1/ipfs/cid/" + path, gatewayUrl);
    }

    [TestMethod]
    public void TryRewrite_UnencodedNonAsciiPath_IsEncodedAsUtf8()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ipfs://cid/\u00E9\U0001F600");

        bool rewritten = CreateRewriter().TryRewrite(CurlUrl.Parse("ipfs://cid/\u00E9\U0001F600"), "http://h:1", out string? gatewayUrl, out _);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        Assert.IsTrue(rewritten);
        diagnostics.Assert("gateway url", "http://h:1/ipfs/cid/%C3%A9%F0%9F%98%80", gatewayUrl);
        Assert.AreEqual("http://h:1/ipfs/cid/%C3%A9%F0%9F%98%80", gatewayUrl);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:1/?a=b")]
    [DataRow("file:///x")]
    [DataRow("file://localhost/x")]
    [DataRow("FILE:///x")]
    [DataRow("http://[::1]:1/")]
    [DataRow("https://[::1]/")]
    public void TryRewrite_UnusableGatewayOption_IsMalformedTargetUrl(string gateway)
    {
        AssertMalformed(TestDiagnostics.For(TestContext), CreateRewriter(), "ipfs://cid/x", gateway);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TryRewrite_OnWindows_DriveLetterFileGatewayOption_IsMalformedTargetUrl()
    {
        AssertMalformed(TestDiagnostics.For(TestContext), CreateRewriter(), "ipfs://cid/x", "file:///C:/x");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TryRewrite_OnLinuxOrMacOS_DriveLetterFileGatewayOption_IsMalformedGatewayOption()
    {
        // curl's non-Windows builds reject a drive letter in a file: URL (lib/urlapi.c,
        // CURLUE_BAD_FILE_URL), so src/tool_ipfs.c cannot parse the gateway option at all.
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("gateway option", "file:///C:/x");

        bool rewritten = CreateRewriter().TryRewrite(CurlUrl.Parse("ipfs://cid/x"), "file:///C:/x", out string? gatewayUrl, out IpfsGatewayFailure? failure);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        diagnostics.Act("failure", failure?.Message);
        diagnostics.Assert("rewritten", false, rewritten);
        Assert.IsFalse(rewritten);
        diagnostics.Assert("gateway url", null, gatewayUrl);
        Assert.IsNull(gatewayUrl);
        diagnostics.Assert("failure is malformed gateway option", true, ReferenceEquals(IpfsGatewayFailure.MalformedGatewayOption, failure));
        Assert.AreSame(IpfsGatewayFailure.MalformedGatewayOption, failure);
    }

    [TestMethod]
    [DataRow(":::")]
    [DataRow("foo://h:1/")]
    [DataRow("http://h:1/ x")]
    [DataRow("garbage ::")]
    [DataRow("http://")]
    [DataRow("http://:1/")]
    [DataRow("http://u@/")]
    [DataRow("ftp://")]
    [DataRow("file://")]
    [DataRow("file://h/x")]
    [DataRow("http://h:99999/")]
    public void TryRewrite_MalformedGatewayOption_IsMalformedGatewayOption(string gateway)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("gateway option", gateway);

        bool rewritten = CreateRewriter().TryRewrite(CurlUrl.Parse("ipfs://cid/x"), gateway, out string? gatewayUrl, out IpfsGatewayFailure? failure);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        diagnostics.Act("failure", failure?.Message);
        diagnostics.Assert("rewritten", false, rewritten);
        Assert.IsFalse(rewritten);
        diagnostics.Assert("gateway url", null, gatewayUrl);
        Assert.IsNull(gatewayUrl);
        diagnostics.Assert("failure is malformed gateway option", true, ReferenceEquals(IpfsGatewayFailure.MalformedGatewayOption, failure));
        Assert.AreSame(IpfsGatewayFailure.MalformedGatewayOption, failure);
    }

    [TestMethod]
    public void MalformedGatewayOption_IsExitFortyThreeWithCurlsMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayFailure failure = IpfsGatewayFailure.MalformedGatewayOption;
        diagnostics.Arrange("failure", "MalformedGatewayOption");
        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("message", failure.Message);

        diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, IpfsGatewayFailure.MalformedGatewayOption.ExitCode);
        diagnostics.Assert("exit code number", 43, (int)failure.ExitCode);
        Assert.AreEqual(43, (int)IpfsGatewayFailure.MalformedGatewayOption.ExitCode);
        diagnostics.Assert("message", "--ipfs-gateway was given a malformed URL", failure.Message);
        Assert.AreEqual("--ipfs-gateway was given a malformed URL", IpfsGatewayFailure.MalformedGatewayOption.Message);
    }

    [TestMethod]
    [DataRow("ipfs://cid/a%01b")]
    [DataRow("ipfs://cid/a%09b")]
    [DataRow("ipfs://cid/a%0Ab")]
    [DataRow("ipfs://cid/a%0Db")]
    [DataRow("ipfs://cid/a%1Fb")]
    [DataRow("ipfs://cid/%00x")]
    public void TryRewrite_PathDecodingToAControlByte_IsMalformedTargetUrl(string url)
    {
        AssertMalformed(TestDiagnostics.For(TestContext), CreateRewriter(), url, "http://127.0.0.1:1/");
    }

    [TestMethod]
    public void TryRewrite_NoGatewayAnywhere_IsGatewayDetectionFailed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter(new() { ["HOME"] = "/home/u" });
        diagnostics.Arrange("environment", "HOME=/home/u");

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse("ipfs://bafyabc/x"), null, out string? gatewayUrl, out IpfsGatewayFailure? failure);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        diagnostics.Act("exit code", failure?.ExitCode);
        diagnostics.Act("message", failure?.Message);
        diagnostics.Assert("rewritten", false, rewritten);
        Assert.IsFalse(rewritten);
        diagnostics.Assert("gateway url", null, gatewayUrl);
        Assert.IsNull(gatewayUrl);
        diagnostics.Assert("exit code", CurlExitCode.FileCouldntReadFile, failure?.ExitCode);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, failure!.ExitCode);
        diagnostics.Assert("message", "IPFS automatic gateway detection failed", failure?.Message);
        Assert.AreEqual("IPFS automatic gateway detection failed", failure!.Message);
    }

    [TestMethod]
    public void TryRewrite_GatewayOption_WinsOverTheEnvironmentAndTheFile()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter(
            new() { ["IPFS_GATEWAY"] = "http://127.0.0.1:2/e", ["HOME"] = "/home/u" },
            new() { [GatewayFile] = "http://127.0.0.1:4/h" });
        diagnostics.Arrange("environment gateway", "http://127.0.0.1:2/e");
        diagnostics.Arrange("file gateway", "http://127.0.0.1:4/h");
        diagnostics.Arrange("gateway option", "http://127.0.0.1:3/o");

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse("ipfs://cid"), "http://127.0.0.1:3/o", out string? gatewayUrl, out _);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        Assert.IsTrue(rewritten);
        diagnostics.Assert("gateway url", "http://127.0.0.1:3/o/ipfs/cid", gatewayUrl);
        Assert.AreEqual("http://127.0.0.1:3/o/ipfs/cid", gatewayUrl);
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:2/e", "http://127.0.0.1:2/e/ipfs/cid/x")]
    [DataRow("http://127.0.0.1:1/g?", "http://127.0.0.1:1/g/ipfs/cid/x")]
    [DataRow("HTTP://127.0.0.1:1", "http://127.0.0.1:1/ipfs/cid/x")]
    public void TryRewrite_EnvironmentVariable_WinsOverTheFile(string variable, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter(
            new() { ["IPFS_GATEWAY"] = variable, ["HOME"] = "/home/u" },
            new() { [GatewayFile] = "http://127.0.0.1:4/h" });
        diagnostics.Arrange("environment gateway", variable);

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse("ipfs://cid/x"), null, out string? gatewayUrl, out _);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        Assert.IsTrue(rewritten);
        diagnostics.Assert("gateway url", expected, gatewayUrl);
        Assert.AreEqual(expected, gatewayUrl);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("http://127.0.0.1:2/?q")]
    [DataRow("foo://h:1/")]
    [DataRow(":::")]
    [DataRow("http://")]
    [DataRow("file:///x")]
    [DataRow("http://127.0.0.1:1/ ")]
    [DataRow("  http://127.0.0.1:1")]
    [DataRow("127.0.0.1:1")]
    public void TryRewrite_UnusableEnvironmentVariable_IsMalformedTargetUrl(string variable)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter(
            new() { ["IPFS_GATEWAY"] = variable, ["HOME"] = "/home/u" },
            new() { [GatewayFile] = "http://127.0.0.1:4/h" });
        diagnostics.Arrange("environment gateway", variable);

        AssertMalformed(diagnostics, rewriter, "ipfs://cid", null);
    }

    [TestMethod]
    [DataRow("/home/u")]
    [DataRow("/home/u/")]
    public void TryRewrite_HomeGatewayFile_UsesItsFirstLine(string home)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter(
            new() { ["HOME"] = home },
            new() { [GatewayFile] = "http://127.0.0.1:4/h\r\nsecond\n" });
        diagnostics.Arrange("home", home);

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse("ipfs://cid"), null, out string? gatewayUrl, out _);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        Assert.IsTrue(rewritten);
        diagnostics.Assert("gateway url", "http://127.0.0.1:4/h/ipfs/cid", gatewayUrl);
        Assert.AreEqual("http://127.0.0.1:4/h/ipfs/cid", gatewayUrl);
    }

    [TestMethod]
    [DataRow("/ip")]
    [DataRow("/ip/")]
    public void TryRewrite_IpfsPathGatewayFile_WinsOverHome(string ipfsPath)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter(
            new() { ["IPFS_PATH"] = ipfsPath, ["HOME"] = "/home/u" },
            new() { ["/ip/gateway"] = "http://127.0.0.1:5/p", [GatewayFile] = "http://127.0.0.1:4/h" });
        diagnostics.Arrange("ipfs path", ipfsPath);

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse("ipfs://cid"), null, out string? gatewayUrl, out _);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        Assert.IsTrue(rewritten);
        diagnostics.Assert("gateway url", "http://127.0.0.1:5/p/ipfs/cid", gatewayUrl);
        Assert.AreEqual("http://127.0.0.1:5/p/ipfs/cid", gatewayUrl);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\nhttp://x")]
    [DataRow("\r\n")]
    public void TryRewrite_GatewayFileWithAnEmptyFirstLine_IsGatewayDetectionFailed(string text)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter(new() { ["IPFS_PATH"] = "/ip" }, new() { ["/ip/gateway"] = text });
        diagnostics.Arrange("gateway file text", text);

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse("ipfs://cid"), null, out _, out IpfsGatewayFailure? failure);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("failure", failure?.Message);
        Assert.IsFalse(rewritten);
        diagnostics.Assert("failure is gateway detection failed", true, ReferenceEquals(IpfsGatewayFailure.GatewayDetectionFailed, failure));
        Assert.AreSame(IpfsGatewayFailure.GatewayDetectionFailed, failure);
    }

    [TestMethod]
    [DataRow("garbage ::")]
    [DataRow("127.0.0.1:6")]
    public void TryRewrite_UnusableGatewayFile_IsMalformedTargetUrl(string text)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayRewriter rewriter = CreateRewriter(new() { ["IPFS_PATH"] = "/ip" }, new() { ["/ip/gateway"] = text });
        diagnostics.Arrange("gateway file text", text);

        AssertMalformed(diagnostics, rewriter, "ipfs://cid", null);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void TryRewrite_NoHome_IsGatewayDetectionFailed(string? home)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Dictionary<string, string?> environment = new() { ["HOME"] = home, ["USERPROFILE"] = "/home/u" };
        IpfsGatewayRewriter rewriter = CreateRewriter(environment, new() { [GatewayFile] = "http://127.0.0.1:4/h" });
        diagnostics.Arrange("home", home ?? "(null)");

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse("ipfs://cid"), null, out _, out IpfsGatewayFailure? failure);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("failure", failure?.Message);
        Assert.IsFalse(rewritten);
        diagnostics.Assert("failure is gateway detection failed", true, ReferenceEquals(IpfsGatewayFailure.GatewayDetectionFailed, failure));
        Assert.AreSame(IpfsGatewayFailure.GatewayDetectionFailed, failure);
    }

    [TestMethod]
    public void TryRewrite_NotAnIpfsUrl_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "http://cid/");

        var exception = Assert.ThrowsExactly<ArgumentException>(() => CreateRewriter().TryRewrite(CurlUrl.Parse("http://cid/"), "http://h", out _, out _));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow("ipfs://cid", true)]
    [DataRow("IPNS://name", true)]
    [DataRow("http://cid", false)]
    public void IsIpfsUrl_NamesIpfsAndIpns(string url, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", url);

        bool isIpfsUrl = IpfsGatewayRewriter.IsIpfsUrl(CurlUrl.Parse(url));

        diagnostics.Act("is ipfs url", isIpfsUrl);
        diagnostics.Assert("is ipfs url", expected, isIpfsUrl);
        Assert.AreEqual(expected, IpfsGatewayRewriter.IsIpfsUrl(CurlUrl.Parse(url)));
    }

    [TestMethod]
    public void IsIpfsUrl_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "(null)");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => IpfsGatewayRewriter.IsIpfsUrl(null!));

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    [TestMethod]
    public void Constructor_NullReader_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("readers", "environment reader null, then file reader null");

        var environmentException = Assert.ThrowsExactly<ArgumentNullException>(() => new IpfsGatewayRewriter(null!, _ => null));
        var fileException = Assert.ThrowsExactly<ArgumentNullException>(() => new IpfsGatewayRewriter(_ => null, null!));

        diagnostics.Act("environment reader exception", environmentException.GetType().Name);
        diagnostics.Act("file reader exception", fileException.GetType().Name);
        diagnostics.Assert("environment reader exception", nameof(ArgumentNullException), environmentException.GetType().Name);
        diagnostics.Assert("file reader exception", nameof(ArgumentNullException), fileException.GetType().Name);
    }

    [TestMethod]
    public void MalformedTargetUrl_IsExitThreeWithCurlsMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IpfsGatewayFailure failure = IpfsGatewayFailure.MalformedTargetUrl;
        diagnostics.Arrange("failure", "MalformedTargetUrl");
        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("message", failure.Message);

        diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.UrlMalformat, IpfsGatewayFailure.MalformedTargetUrl.ExitCode);
        diagnostics.Assert("message", "malformed target URL", failure.Message);
        Assert.AreEqual("malformed target URL", IpfsGatewayFailure.MalformedTargetUrl.Message);
    }

    private static void AssertMalformed(TestDiagnostics diagnostics, IpfsGatewayRewriter rewriter, string url, string? gateway)
    {
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("gateway option", gateway ?? "(none)");

        bool rewritten = rewriter.TryRewrite(CurlUrl.Parse(url), gateway, out string? gatewayUrl, out IpfsGatewayFailure? failure);

        diagnostics.Act("rewritten", rewritten);
        diagnostics.Act("gateway url", gatewayUrl);
        diagnostics.Act("failure", failure?.Message);
        diagnostics.Assert("rewritten", false, rewritten);
        Assert.IsFalse(rewritten);
        diagnostics.Assert("gateway url", null, gatewayUrl);
        Assert.IsNull(gatewayUrl);
        diagnostics.Assert("failure is malformed target url", true, ReferenceEquals(IpfsGatewayFailure.MalformedTargetUrl, failure));
        Assert.AreSame(IpfsGatewayFailure.MalformedTargetUrl, failure);
    }

    private static IpfsGatewayRewriter CreateRewriter(
        Dictionary<string, string?>? environment = null,
        Dictionary<string, string>? files = null) =>
        new(
            name => environment?.GetValueOrDefault(name),
            path => files?.GetValueOrDefault(path));
}
