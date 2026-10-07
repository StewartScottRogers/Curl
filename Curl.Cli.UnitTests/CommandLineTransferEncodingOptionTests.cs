using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoTransferEncodingOptions_LeavesThemAtCurlDefaults()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("compressed", false, Recorded(result)?.Compressed);
        Diagnostics.Assert("raw", false, Recorded(result)?.Raw);
        Diagnostics.Assert("transfer encoding", false, Recorded(result)?.TransferEncoding);
        Diagnostics.Assert("ignore content length", false, Recorded(result)?.IgnoreContentLength);
        Diagnostics.Assert("path as is", false, Recorded(result)?.PathAsIs);
        Diagnostics.Assert("allow HTTP/0.9 reply", false, Recorded(result)?.AllowHttp09Reply);
        Diagnostics.Assert("request target", null, Recorded(result)?.RequestTarget);
        Diagnostics.Assert("HTTP version", null, Recorded(result)?.HttpVersion);
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
        CommandLineOptions options = Accept("--compressed");

        Diagnostics.Assert("compressed", true, options.Compressed);
        Assert.IsTrue(options.Compressed);
    }

    [TestMethod]
    public void Parse_Raw_SetsRaw()
    {
        CommandLineOptions options = Accept("--raw");

        Diagnostics.Assert("raw", true, options.Raw);
        Assert.IsTrue(options.Raw);
    }

    [TestMethod]
    public void Parse_TrEncoding_SetsTransferEncoding()
    {
        CommandLineOptions options = Accept("--tr-encoding");

        Diagnostics.Assert("transfer encoding", true, options.TransferEncoding);
        Assert.IsTrue(options.TransferEncoding);
    }

    [TestMethod]
    public void Parse_IgnoreContentLength_SetsIgnoreContentLength()
    {
        CommandLineOptions options = Accept("--ignore-content-length");

        Diagnostics.Assert("ignore content length", true, options.IgnoreContentLength);
        Assert.IsTrue(options.IgnoreContentLength);
    }

    [TestMethod]
    public void Parse_PathAsIs_SetsPathAsIs()
    {
        CommandLineOptions options = Accept("--path-as-is");

        Diagnostics.Assert("path as is", true, options.PathAsIs);
        Assert.IsTrue(options.PathAsIs);
    }

    // curl accepts and ignores a value attached to a flag: --compressed=x connects (measured).
    [TestMethod]
    public void Parse_CompressedWithAttachedValue_SetsCompressed()
    {
        CommandLineOptions options = Accept("--compressed=x");

        Diagnostics.Assert("compressed", true, options.Compressed);
        Assert.IsTrue(options.Compressed);
    }

    [TestMethod]
    public void Parse_NoCompressedAfterCompressed_TurnsCompressedOff()
    {
        CommandLineOptions options = Accept("--compressed", "--no-compressed");

        Diagnostics.Assert("compressed", false, options.Compressed);
        Assert.IsFalse(options.Compressed);
    }

    [TestMethod]
    public void Parse_NoCompressedWithAttachedValue_TurnsCompressedOff()
    {
        CommandLineOptions options = Accept("--compressed", "--no-compressed=x");

        Diagnostics.Assert("compressed", false, options.Compressed);
        Assert.IsFalse(options.Compressed);
    }

    [TestMethod]
    public void Parse_CompressedAfterNoCompressed_SetsCompressed()
    {
        CommandLineOptions options = Accept("--no-compressed", "--compressed");

        Diagnostics.Assert("compressed", true, options.Compressed);
        Assert.IsTrue(options.Compressed);
    }

    [TestMethod]
    public void Parse_NoRawAfterRaw_TurnsRawOff()
    {
        CommandLineOptions options = Accept("--raw", "--no-raw");

        Diagnostics.Assert("raw", false, options.Raw);
        Assert.IsFalse(options.Raw);
    }

    [TestMethod]
    public void Parse_NoTrEncodingAfterTrEncoding_TurnsTransferEncodingOff()
    {
        CommandLineOptions options = Accept("--tr-encoding", "--no-tr-encoding");

        Diagnostics.Assert("transfer encoding", false, options.TransferEncoding);
        Assert.IsFalse(options.TransferEncoding);
    }

    [TestMethod]
    public void Parse_NoIgnoreContentLengthAfterIgnoreContentLength_TurnsItOff()
    {
        CommandLineOptions options = Accept("--ignore-content-length", "--no-ignore-content-length");

        Diagnostics.Assert("ignore content length", false, options.IgnoreContentLength);
        Assert.IsFalse(options.IgnoreContentLength);
    }

    [TestMethod]
    public void Parse_NoPathAsIsAfterPathAsIs_TurnsPathAsIsOff()
    {
        CommandLineOptions options = Accept("--path-as-is", "--no-path-as-is");

        Diagnostics.Assert("path as is", false, options.PathAsIs);
        Assert.IsFalse(options.PathAsIs);
    }

    [TestMethod]
    public void Parse_Http09_AllowsAnHttp09Reply()
    {
        CommandLineOptions options = Accept("--http0.9");

        Diagnostics.Assert("allow HTTP/0.9 reply", true, options.AllowHttp09Reply);
        Assert.IsTrue(options.AllowHttp09Reply);
    }

    [TestMethod]
    public void Parse_NoHttp09AfterHttp09_RefusesAnHttp09ReplyAgain()
    {
        CommandLineOptions options = Accept("--http0.9", "--no-http0.9");

        Diagnostics.Assert("allow HTTP/0.9 reply", false, options.AllowHttp09Reply);
        Assert.IsFalse(options.AllowHttp09Reply);
    }

    [TestMethod]
    public void Parse_NoHttp09WithAttachedValueAfterHttp09_IgnoresTheValueAndTurnsItOff()
    {
        CommandLineOptions options = Accept("--http0.9", "--no-http0.9=x");

        Diagnostics.Assert("allow HTTP/0.9 reply", false, options.AllowHttp09Reply);
        Assert.IsFalse(options.AllowHttp09Reply);
    }

    [TestMethod]
    public void Parse_Http09AfterNoHttp09_AllowsAnHttp09Reply()
    {
        CommandLineOptions options = Accept("--no-http0.9", "--http0.9");

        Diagnostics.Assert("allow HTTP/0.9 reply", true, options.AllowHttp09Reply);
        Assert.IsTrue(options.AllowHttp09Reply);
    }

    [TestMethod]
    public void Parse_RequestTarget_RecordsTheTarget()
    {
        CommandLineOptions options = Accept("--request-target", "*");

        Diagnostics.Assert("request target", "*", options.RequestTarget);
        Assert.AreEqual("*", options.RequestTarget);
    }

    [TestMethod]
    public void Parse_RequestTargetWithAttachedValue_RecordsTheTarget()
    {
        CommandLineOptions options = Accept("--request-target=/a/../b");

        Diagnostics.Assert("request target", "/a/../b", options.RequestTarget);
        Assert.AreEqual("/a/../b", options.RequestTarget);
    }

    [TestMethod]
    public void Parse_RequestTargetGivenTwice_KeepsTheLast()
    {
        CommandLineOptions options = Accept("--request-target", "/first", "--request-target", "/second");

        Diagnostics.Assert("request target", "/second", options.RequestTarget);
        Assert.AreEqual("/second", options.RequestTarget);
    }

    [TestMethod]
    public void Parse_EmptyRequestTarget_IsRefusedAsBlank()
    {
        CommandLineParseResult result = Parse(["--request-target=", Url]);

        AssertRefused(result, "curl: option --request-target=: blank argument where content is expected");
    }

    // Measured: curl http://127.0.0.1:1/ --request-target (exit 2).
    [TestMethod]
    public void Parse_RequestTargetLast_IsRefusedAsNeedingParameter()
    {
        CommandLineParseResult result = Parse([Url, "--request-target"]);

        AssertRefused(result, "curl: option --request-target: requires parameter");
    }

    [TestMethod]
    [DataRow("-0")]
    [DataRow("--http1.0")]
    public void Parse_Http10_SelectsHttp10(string spelling)
    {
        CommandLineOptions options = Accept(spelling);

        Diagnostics.Assert("HTTP version", RequestedHttpVersion.Http10, options.HttpVersion);
        Assert.AreEqual(RequestedHttpVersion.Http10, options.HttpVersion);
    }

    [TestMethod]
    public void Parse_Http11_SelectsHttp11()
    {
        CommandLineOptions options = Accept("--http1.1");

        Diagnostics.Assert("HTTP version", RequestedHttpVersion.Http11, options.HttpVersion);
        Assert.AreEqual(RequestedHttpVersion.Http11, options.HttpVersion);
    }

    [TestMethod]
    public void Parse_Http10InABundle_SelectsHttp10()
    {
        CommandLineOptions options = Accept("-s0");

        Diagnostics.Assert("HTTP version", RequestedHttpVersion.Http10, options.HttpVersion);
        Assert.AreEqual(RequestedHttpVersion.Http10, options.HttpVersion);
    }

    [TestMethod]
    public void Parse_Http11ThenHttp10_KeepsTheLastAndWarns()
    {
        CommandLineParseResult result = Parse(["--http1.1", "-0", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("HTTP version", RequestedHttpVersion.Http10, Recorded(result)?.HttpVersion);
        AssertWarningLines([OverridesWarning], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http10, result.Options.HttpVersion);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_Http10ThenHttp11ThenHttp10_WarnsTwice()
    {
        CommandLineParseResult result = Parse(["-0", "--http1.1", "-0", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("HTTP version", RequestedHttpVersion.Http10, Recorded(result)?.HttpVersion);
        AssertWarningLines([OverridesWarning, OverridesWarning], result);
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
        CommandLineParseResult result = Parse([first, second, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HttpVersionOverriddenAfterSilent_DoesNotWarn()
    {
        CommandLineParseResult result = Parse(["-s", "--http1.0", "--http1.1", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("HTTP version", RequestedHttpVersion.Http11, Recorded(result)?.HttpVersion);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http11, result.Options.HttpVersion);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HttpVersionOverriddenBeforeSilent_StillWarns()
    {
        CommandLineParseResult result = Parse(["--http1.0", "--http1.1", "-s", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarningLines([OverridesWarning], result);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--http2", RequestedHttpVersion.Http2)]
    [DataRow("--http2=x", RequestedHttpVersion.Http2)]
    [DataRow("--http2-prior-knowledge", RequestedHttpVersion.Http2PriorKnowledge)]
    public void Parse_Http2Option_IsAcceptedByTheOpenSslBuild(string spelling, RequestedHttpVersion expected)
    {
        CommandLineOptions options = Accept(spelling);

        Diagnostics.Assert("HTTP version", expected, options.HttpVersion);
        Assert.AreEqual(expected, options.HttpVersion);
    }

    [TestMethod]
    [DataRow("--http1.1", "--http2", RequestedHttpVersion.Http2)]
    [DataRow("--http2", "-0", RequestedHttpVersion.Http10)]
    [DataRow("--http2", "--http2-prior-knowledge", RequestedHttpVersion.Http2PriorKnowledge)]
    public void Parse_Http2OptionAndAnotherVersion_KeepsTheLastAndWarns(string first, string second, RequestedHttpVersion expected)
    {
        CommandLineParseResult result = Parse([first, second, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("HTTP version", expected, Recorded(result)?.HttpVersion);
        AssertWarningLines([OverridesWarning], result);
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
        CommandLineParseResult result = Parse([spelling, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("HTTP version", expected, Recorded(result)?.HttpVersion);
        AssertWarningLines([], result);
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
        CommandLineParseResult result = Parse([first, second, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("HTTP version", expected, Recorded(result)?.HttpVersion);
        AssertWarningLines([OverridesWarning], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expected, result.Options.HttpVersion);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--http3")]
    [DataRow("--http3-only")]
    public void Parse_Http3OptionTwice_KeepsItWithoutAWarning(string spelling)
    {
        CommandLineParseResult result = Parse([spelling, spelling, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertWarningLines([], result);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_Http3AfterSilent_IsAcceptedWithoutAWarning()
    {
        CommandLineParseResult result = Parse(["-s", "--http2", "--http3", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("HTTP version", RequestedHttpVersion.Http3, Recorded(result)?.HttpVersion);
        AssertWarningLines([], result);
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
        CommandLineParseResult result = Parse([spelling, Url]);

        AssertRefused(result, $"curl: option {spelling}: {CannotBeReversed}");
    }

    [TestMethod]
    public void Parse_Http3OnlyInAConfigFile_IsAccepted()
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.txt"] = "http3-only\n"u8.ToArray();
        string[] arguments = ["-K", "k.txt", Url];
        Diagnostics.ArrangeArguments(arguments);
        Diagnostics.Bytes("k.txt", reader.Files["k.txt"]);

        CommandLineParseResult result = OpenSslBuildParser.Parse(arguments, _ => true, ConsolePasswordPrompt.ForProcessConsole, reader);
        WriteOutcome(result);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("HTTP version", RequestedHttpVersion.Http3Only, Recorded(result)?.HttpVersion);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(RequestedHttpVersion.Http3Only, result.Options.HttpVersion);
    }

    [TestMethod]
    public void InstalledLibcurlDoesNotSupport_NullSpelledOption_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("spelled option", null);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.InstalledLibcurlDoesNotSupport(null!));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    /// <summary>
    /// Returns the parsed options, or null for a refusal, for diagnostic lines written before the test asserts
    /// acceptance, without making the compiler treat <see cref="CommandLineParseResult.Options"/> as possibly null.
    /// </summary>
    private static CommandLineOptions? Recorded(CommandLineParseResult result) => result.Options;

    /// <summary>Parses <paramref name="arguments"/> as the OpenSSL build, writing them, the outcome and the option values as diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = OpenSslBuildParser.Parse(arguments);
        WriteOutcome(result);
        return result;
    }

    private void WriteOutcome(CommandLineParseResult result)
    {
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("compressed", result.Options.Compressed);
            Diagnostics.Act("raw", result.Options.Raw);
            Diagnostics.Act("transfer encoding", result.Options.TransferEncoding);
            Diagnostics.Act("ignore content length", result.Options.IgnoreContentLength);
            Diagnostics.Act("path as is", result.Options.PathAsIs);
            Diagnostics.Act("allow HTTP/0.9 reply", result.Options.AllowHttp09Reply);
            Diagnostics.Act("request target", result.Options.RequestTarget);
            Diagnostics.Act("HTTP version", result.Options.HttpVersion);
        }
    }

    private CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = Parse([.. arguments, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        return result.Options;
    }

    private void AssertWarningLines(IEnumerable<string> expected, CommandLineParseResult result) =>
        Diagnostics.Assert("warning lines", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(result.WarningLines));

    private void AssertRefused(CommandLineParseResult result, string optionLine)
    {
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [optionLine, TryHelp]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { optionLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
