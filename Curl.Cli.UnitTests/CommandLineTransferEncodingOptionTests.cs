using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins the transfer-encoding and HTTP version options: <c>--compressed</c>, <c>--raw</c>,
/// <c>--tr-encoding</c>, <c>--ignore-content-length</c> and <c>--path-as-is</c> (each negatable),
/// <c>--http0.9</c> (negatable), <c>--request-target</c>, <c>-0</c>/<c>--http1.0</c> and <c>--http1.1</c>, and the refusal of
/// <c>--http2</c>, <c>--http2-prior-knowledge</c>, <c>--http3</c> and <c>--http3-only</c> (ADR-0017).
/// Every refusal and warning line was measured with <c>/mingw64/bin/curl</c> 8.21.0 against
/// <c>http://127.0.0.1:1/</c> on 2026-09-26.
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
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

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
        CommandLineParseResult result = CommandLineParser.Parse(["--request-target=", Url]);

        AssertRefused(result, "curl: option --request-target=: blank argument where content is expected");
    }

    // Measured: curl http://127.0.0.1:1/ --request-target (exit 2).
    [TestMethod]
    public void Parse_RequestTargetLast_IsRefusedAsNeedingParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "--request-target"]);

        AssertRefused(result, "curl: option --request-target: requires parameter");
    }

    [TestMethod]
    [DataRow("-0")]
    [DataRow("--http1.0")]
    public void Parse_Http10_SelectsHttp10(string spelling)
    {
        Assert.AreEqual(HttpVersionPreference.Http10, Accept(spelling).HttpVersion);
    }

    [TestMethod]
    public void Parse_Http11_SelectsHttp11()
    {
        Assert.AreEqual(HttpVersionPreference.Http11, Accept("--http1.1").HttpVersion);
    }

    [TestMethod]
    public void Parse_Http10InABundle_SelectsHttp10()
    {
        Assert.AreEqual(HttpVersionPreference.Http10, Accept("-s0").HttpVersion);
    }

    [TestMethod]
    public void Parse_Http11ThenHttp10_KeepsTheLastAndWarns()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--http1.1", "-0", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(HttpVersionPreference.Http10, result.Options.HttpVersion);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_Http10ThenHttp11ThenHttp10_WarnsTwice()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-0", "--http1.1", "-0", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(HttpVersionPreference.Http10, result.Options.HttpVersion);
        CollectionAssert.AreEqual(new[] { OverridesWarning, OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("-0", "-0")]
    [DataRow("--http1.1", "--http1.1")]
    [DataRow("--http1.0", "--http1.0=x")]
    public void Parse_SameHttpVersionTwice_DoesNotWarn(string first, string second)
    {
        CommandLineParseResult result = CommandLineParser.Parse([first, second, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HttpVersionOverriddenAfterSilent_DoesNotWarn()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--http1.0", "--http1.1", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(HttpVersionPreference.Http11, result.Options.HttpVersion);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_HttpVersionOverriddenBeforeSilent_StillWarns()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--http1.0", "--http1.1", "-s", Url]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { OverridesWarning }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--http2")]
    [DataRow("--http2-prior-knowledge")]
    [DataRow("--http3")]
    [DataRow("--http3-only")]
    [DataRow("--http2=x")]
    public void Parse_UnsupportedHttpVersion_IsRefusedAsNotSupported(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        AssertRefused(result, $"curl: option {spelling}: {NotSupported}");
    }

    [TestMethod]
    public void Parse_Http2AfterSilent_IsStillRefusedWithBothLines()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--http2", Url]);

        AssertRefused(result, $"curl: option --http2: {NotSupported}");
    }

    [TestMethod]
    public void Parse_Http2BeforeAnUnknownOption_IsRefusedAsNotSupported()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--http2", "--bogus", Url]);

        AssertRefused(result, $"curl: option --http2: {NotSupported}");
    }

    [TestMethod]
    public void Parse_Http2AfterHttp10_IsRefusedWithoutAWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-0", "--http2", Url]);

        AssertRefused(result, $"curl: option --http2: {NotSupported}");
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
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

        AssertRefused(result, $"curl: option {spelling}: {CannotBeReversed}");
    }

    // Measured: printf 'http2\n' > k.txt; curl -K k.txt http://127.0.0.1:1/ (exit 2).
    [TestMethod]
    public void Parse_Http2InAConfigFile_IsRefusedAsNotSupportedWithCurlsWrappedLines()
    {
        RecordingDataFileReader reader = new();
        reader.Files["k.txt"] = "http2\n"u8.ToArray();

        CommandLineParseResult result = CommandLineParser.Parse(["-K", "k.txt", Url], _ => true, ConsolePasswordPrompt.ForProcessConsole, reader);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: k.txt:1 config file option 'http2' the installed libcurl version does ",
                "curl: not support this",
                $"curl: option -K: {NotSupported}",
                TryHelp,
            },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void UnsupportedFlag_NullLongName_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineOption.UnsupportedFlag(null!));

        Assert.AreEqual("longName", exception.ParamName);
    }

    [TestMethod]
    public void UnsupportedFlag_Created_TakesNoValueHasNoShortLetterAndCannotBeNegated()
    {
        CommandLineOption option = CommandLineOption.UnsupportedFlag("http9");

        Assert.AreEqual("http9", option.LongName);
        Assert.IsNull(option.ShortName);
        Assert.IsFalse(option.TakesValue);
        Assert.IsNull(option.Negate);
    }

    [TestMethod]
    public void InstalledLibcurlDoesNotSupport_NullSpelledOption_ThrowsArgumentNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.InstalledLibcurlDoesNotSupport(null!));
    }

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse([.. arguments, Url]);

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
