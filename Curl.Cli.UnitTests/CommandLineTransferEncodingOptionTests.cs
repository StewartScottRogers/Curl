using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins the transfer-encoding and HTTP version options: <c>--compressed</c>, <c>--raw</c>,
/// <c>--tr-encoding</c>, <c>--ignore-content-length</c> and <c>--path-as-is</c> (each negatable),
/// <c>--http0.9</c> (negatable), <c>--request-target</c>, <c>-0</c>/<c>--http1.0</c>, <c>--http1.1</c>, <c>--http2</c> and
/// <c>--http2-prior-knowledge</c> (ADR-0141), and <c>--http3</c> and <c>--http3-only</c> (ADR-0144).
/// Every refusal and warning line was measured with <c>/mingw64/bin/curl</c> 8.21.0 against
/// <c>http://127.0.0.1:1/</c> on 2026-09-26, and the HTTP/3 ones with curl.se's 8.18.0 ngtcp2 build.
/// </summary>
[TestClass]
public sealed class CommandLineTransferEncodingOptionTests
{
    private const string Url = "http://127.0.0.1:1/";
    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";
    private const string NotSupported = "the installed libcurl version does not support this";
    private const string CannotBeReversed = "the given option cannot be reversed with a --no- prefix";
    private const string OverridesWarning = "Warning: Overrides previous HTTP version option";

    [TestMethod]
    public void Parse_NoTransferEncodingOptions_LeavesThemAtCurlDefaults()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.Compressed);
        Assert.IsFalse(result.Options.Raw);
        Assert.IsFalse(result.Options.TransferEncoding);
        Assert.IsFalse(result.Options.IgnoreContentLength);
        Assert.IsFalse(result.Options.PathAsIs);
        Assert.IsFalse(result.Options.AllowHttp09Reply);
        Assert.IsNull(result.Options.RequestTarget);
        Assert.IsNull(result.Options.HttpVersion);
    }

    [TestMethod]
    public void Parse_Compressed_SetsCompressed()
    {
        Assert.IsTrue(Accept("--compressed").Compressed);
    }

    [TestMethod]
    public void Parse_Raw_SetsRaw()
    {
        Assert.IsTrue(Accept("--raw").Raw);
    }

    [TestMethod]
    public void Parse_TrEncoding_SetsTransferEncoding()
    {
        Assert.IsTrue(Accept("--tr-encoding").TransferEncoding);
    }

    [TestMethod]
    public void Parse_IgnoreContentLength_SetsIgnoreContentLength()
    {
        Assert.IsTrue(Accept("--ignore-content-length").IgnoreContentLength);
    }

    [TestMethod]
    public void Parse_PathAsIs_SetsPathAsIs()
    {
        Assert.IsTrue(Accept("--path-as-is").PathAsIs);
    }

    // curl accepts and ignores a value attached to a flag: --compressed=x connects (measured).
    [TestMethod]
    public void Parse_CompressedWithAttachedValue_SetsCompressed()
    {
        Assert.IsTrue(Accept("--compressed=x").Compressed);
    }

    [TestMethod]
    public void Parse_NoCompressedAfterCompressed_TurnsCompressedOff()
    {
        Assert.IsFalse(Accept("--compressed", "--no-compressed").Compressed);
    }

    [TestMethod]
    public void Parse_NoCompressedWithAttachedValue_TurnsCompressedOff()
    {
        Assert.IsFalse(Accept("--compressed", "--no-compressed=x").Compressed);
    }

    [TestMethod]
    public void Parse_CompressedAfterNoCompressed_SetsCompressed()
    {
        Assert.IsTrue(Accept("--no-compressed", "--compressed").Compressed);
    }

    [TestMethod]
    public void Parse_NoRawAfterRaw_TurnsRawOff()
    {
        Assert.IsFalse(Accept("--raw", "--no-raw").Raw);
    }

    [TestMethod]
    public void Parse_NoTrEncodingAfterTrEncoding_TurnsTransferEncodingOff()
    {
        Assert.IsFalse(Accept("--tr-encoding", "--no-tr-encoding").TransferEncoding);
    }

    [TestMethod]
    public void Parse_NoIgnoreContentLengthAfterIgnoreContentLength_TurnsItOff()
    {
        Assert.IsFalse(Accept("--ignore-content-length", "--no-ignore-content-length").IgnoreContentLength);
    }

    [TestMethod]
    public void Parse_NoPathAsIsAfterPathAsIs_TurnsPathAsIsOff()
    {
        Assert.IsFalse(Accept("--path-as-is", "--no-path-as-is").PathAsIs);
    }

    [TestMethod]
    public void Parse_Http09_AllowsAnHttp09Reply()
    {
        Assert.IsTrue(Accept("--http0.9").AllowHttp09Reply);
    }

    [TestMethod]
    public void Parse_NoHttp09AfterHttp09_RefusesAnHttp09ReplyAgain()
    {
        Assert.IsFalse(Accept("--http0.9", "--no-http0.9").AllowHttp09Reply);
    }

    [TestMethod]
    public void Parse_NoHttp09WithAttachedValueAfterHttp09_IgnoresTheValueAndTurnsItOff()
    {
        Assert.IsFalse(Accept("--http0.9", "--no-http0.9=x").AllowHttp09Reply);
    }

    [TestMethod]
    public void Parse_Http09AfterNoHttp09_AllowsAnHttp09Reply()
    {
        Assert.IsTrue(Accept("--no-http0.9", "--http0.9").AllowHttp09Reply);
    }

    [TestMethod]
    public void Parse_RequestTarget_RecordsTheTarget()
    {
        Assert.AreEqual("*", Accept("--request-target", "*").RequestTarget);
    }

    [TestMethod]
    public void Parse_RequestTargetWithAttachedValue_RecordsTheTarget()
    {
        Assert.AreEqual("/a/../b", Accept("--request-target=/a/../b").RequestTarget);
    }

    [TestMethod]
    public void Parse_RequestTargetGivenTwice_KeepsTheLast()
    {
        Assert.AreEqual("/second", Accept("--request-target", "/first", "--request-target", "/second").RequestTarget);
    }

    [TestMethod]
    public void Parse_EmptyRequestTarget_IsRefusedAsBlank()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["--request-target=", Url]);

        AssertRefused(result, "curl: option --request-target=: blank argument where content is expected");
    }

    // Measured: curl http://127.0.0.1:1/ --request-target (exit 2).
    [TestMethod]
    public void Parse_RequestTargetLast_IsRefusedAsNeedingParameter()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([Url, "--request-target"]);

        AssertRefused(result, "curl: option --request-target: requires parameter");
    }

    [TestMethod]
    [DataRow("-0")]
    [DataRow("--http1.0")]
    public void Parse_Http10_SelectsHttp10(string spelling)
    {
        Assert.AreEqual(RequestedHttpVersion.Http10, Accept(spelling).HttpVersion);
    }

    [TestMethod]
    public void Parse_Http11_SelectsHttp11()
    {
        Assert.AreEqual(RequestedHttpVersion.Http11, Accept("--http1.1").HttpVersion);
    }

    [TestMethod]
    public void Parse_Http10InABundle_SelectsHttp10()
    {
        Assert.AreEqual(RequestedHttpVersion.Http10, Accept("-s0").HttpVersion);
    }

    [TestMethod]
    public void Parse_Http11ThenHttp10_KeepsTheLastAndWarns()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["--http1.1", "-0", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http10, result.Options.HttpVersion);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_Http10ThenHttp11ThenHttp10_WarnsTwice()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["-0", "--http1.1", "-0", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http10, result.Options.HttpVersion);
        CollectionAssert.AreEqual(new[] { OverridesWarning, OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("-0", "-0")]
    [DataRow("--http1.1", "--http1.1")]
    [DataRow("--http1.0", "--http1.0=x")]
    public void Parse_SameHttpVersionTwice_DoesNotWarn(string first, string second)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HttpVersionOverriddenAfterSilent_DoesNotWarn()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["-s", "--http1.0", "--http1.1", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http11, result.Options.HttpVersion);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HttpVersionOverriddenBeforeSilent_StillWarns()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["--http1.0", "--http1.1", "-s", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--http2", RequestedHttpVersion.Http2)]
    [DataRow("--http2=x", RequestedHttpVersion.Http2)]
    [DataRow("--http2-prior-knowledge", RequestedHttpVersion.Http2PriorKnowledge)]
    public void Parse_Http2Option_IsAcceptedByTheOpenSslBuild(string spelling, RequestedHttpVersion expected)
    {
        Assert.AreEqual(expected, Accept(spelling).HttpVersion);
    }

    [TestMethod]
    [DataRow("--http1.1", "--http2", RequestedHttpVersion.Http2)]
    [DataRow("--http2", "-0", RequestedHttpVersion.Http10)]
    [DataRow("--http2", "--http2-prior-knowledge", RequestedHttpVersion.Http2PriorKnowledge)]
    public void Parse_Http2OptionAndAnotherVersion_KeepsTheLastAndWarns(string first, string second, RequestedHttpVersion expected)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.HttpVersion);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    // Measured with curl.se's 8.18.0 ngtcp2 build (ADR-0144) on 2026-09-28: both options are
    // accepted, an attached value is ignored as for every flag, and they are one setting with the
    // other version options, so a different earlier one draws the overrides warning.
    [TestMethod]
    [DataRow("--http3", RequestedHttpVersion.Http3)]
    [DataRow("--http3=x", RequestedHttpVersion.Http3)]
    [DataRow("--http3-only", RequestedHttpVersion.Http3Only)]
    public void Parse_Http3Option_IsAcceptedByTheOpenSslBuild(string spelling, RequestedHttpVersion expected)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([spelling, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.HttpVersion);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--http1.1", "--http3", RequestedHttpVersion.Http3)]
    [DataRow("--http3", "--http1.1", RequestedHttpVersion.Http11)]
    [DataRow("--http3", "--http2", RequestedHttpVersion.Http2)]
    [DataRow("--http3", "--http3-only", RequestedHttpVersion.Http3Only)]
    [DataRow("--http3-only", "--http1.1", RequestedHttpVersion.Http11)]
    [DataRow("--http2", "--http3-only", RequestedHttpVersion.Http3Only)]
    [DataRow("-0", "--http3", RequestedHttpVersion.Http3)]
    public void Parse_Http3OptionAndAnotherVersion_KeepsTheLastAndWarns(string first, string second, RequestedHttpVersion expected)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.HttpVersion);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--http3")]
    [DataRow("--http3-only")]
    public void Parse_Http3OptionTwice_KeepsItWithoutAWarning(string spelling)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([spelling, spelling, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_Http3AfterSilent_IsAcceptedWithoutAWarning()
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse(["-s", "--http2", "--http3", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http3, result.Options.HttpVersion);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--no-http1.0")]
    [DataRow("--no-http1.0=x")]
    [DataRow("--no-http1.1")]
    [DataRow("--no-http2")]
    [DataRow("--no-http2-prior-knowledge")]
    [DataRow("--no-http3")]
    [DataRow("--no-http3-only")]
    [DataRow("--no-request-target")]
    [DataRow("--no-request-target=x")]
    public void Parse_NotReversibleNoSpelling_IsRefusedAsNotReversible(string spelling)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([spelling, Url]);

        AssertRefused(result, $"curl: option {spelling}: {CannotBeReversed}");
    }

    [TestMethod]
    public void Parse_Http3OnlyInAConfigFile_IsAccepted()
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.txt"] = "http3-only\n"u8.ToArray();

        CommandLineParseResult result = OpenSslBuildParser.Parse(["-K", "k.txt", Url], _ => true, ConsolePasswordPrompt.ForProcessConsole, reader);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http3Only, result.Options.HttpVersion);
    }

    [TestMethod]
    public void InstalledLibcurlDoesNotSupport_NullSpelledOption_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.InstalledLibcurlDoesNotSupport(null!));
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = OpenSslBuildParser.Parse([.. arguments, Url]);

        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private static void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
