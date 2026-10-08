using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--no-</c> negation as curl 8.21.0 does it, measured against the local curl 8.21.0 on
/// 2026-09-26: <c>--no-&lt;name&gt;</c> turns off a <see cref="CommandLineOption.NegatableFlag"/> row
/// and the last spelling wins; the <c>--no-</c> spelling of any other row is refused as not
/// reversible; a <c>--no-</c> name with no row after it is unknown.
/// </summary>
[TestClass]
public sealed class CommandLineNegationTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string CannotBeReversed = "the given option cannot be reversed with a --no- prefix";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_SilentThenNoSilent_IsNotSilent()
    {
        CommandLineParseResult result = Parse(["-s", "--no-silent", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("Silent", false, CommandLineParseDiagnostics.Peek(result.Options)?.Silent);
        Assert.IsFalse(result.Options.Silent);
    }

    [TestMethod]
    public void Parse_NoSilentThenSilent_IsSilent()
    {
        CommandLineParseResult result = Parse(["--no-silent", "-s", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("Silent", true, CommandLineParseDiagnostics.Peek(result.Options)?.Silent);
        Assert.IsTrue(result.Options.Silent);
    }

    [TestMethod]
    public void Parse_ShowErrorThenNoShowError_DoesNotShowErrors()
    {
        CommandLineParseResult result = Parse(["-S", "--no-show-error", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("ShowError", false, CommandLineParseDiagnostics.Peek(result.Options)?.ShowError);
        Assert.IsFalse(result.Options.ShowError);
    }

    [TestMethod]
    public void Parse_NoShowErrorThenShowError_ShowsErrors()
    {
        CommandLineParseResult result = Parse(["--no-show-error", "-S", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("ShowError", true, CommandLineParseDiagnostics.Peek(result.Options)?.ShowError);
        Assert.IsTrue(result.Options.ShowError);
    }

    [TestMethod]
    public void Parse_InsecureThenNoInsecure_VerifiesCertificates()
    {
        CommandLineParseResult result = Parse(["-k", "--no-insecure", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("Insecure", false, CommandLineParseDiagnostics.Peek(result.Options)?.Insecure);
        Assert.IsFalse(result.Options.Insecure);
    }

    [TestMethod]
    public void Parse_TftpNoOptionsThenItsNegation_SendsOptions()
    {
        CommandLineParseResult result = Parse(["--tftp-no-options", "--no-tftp-no-options", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("TftpNoOptions", false, CommandLineParseDiagnostics.Peek(result.Options)?.TftpNoOptions);
        Assert.IsFalse(result.Options.TftpNoOptions);
    }

    [TestMethod]
    public void Parse_NoSilentWithAttachedValue_IgnoresTheValue()
    {
        CommandLineParseResult result = Parse(["-s", "--no-silent=x", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("Silent", false, CommandLineParseDiagnostics.Peek(result.Options)?.Silent);
        Assert.IsFalse(result.Options.Silent);
        Diagnostics.Assert("urls", CommandLineParseDiagnostics.QuoteEach([Url]), CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Options)?.Urls ?? []));
        CollectionAssert.AreEqual(new[] { Url }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_NoOutput_IsRefusedAsNotReversible()
    {
        CommandLineParseResult result = Parse(["--no-output", "x", Url]);

        AssertRefused(result, "curl: option --no-output: " + CannotBeReversed);
    }

    [TestMethod]
    public void Parse_NoOutputAsLastArgument_IsRefusedAsNotReversibleNotAsMissingParameter()
    {
        CommandLineParseResult result = Parse(["--no-output"]);

        AssertRefused(result, "curl: option --no-output: " + CannotBeReversed);
    }

    [TestMethod]
    public void Parse_NoOutputWithAttachedValue_NamesTheWholeArgument()
    {
        CommandLineParseResult result = Parse(["--no-output=x", Url]);

        AssertRefused(result, "curl: option --no-output=x: " + CannotBeReversed);
    }

    [TestMethod]
    public void Parse_NoTls12_FlagThatCannotBeNegatedIsRefusedAsNotReversible()
    {
        CommandLineParseResult result = Parse(["--no-tlsv1.2", Url]);

        AssertRefused(result, "curl: option --no-tlsv1.2: " + CannotBeReversed);
    }

    [TestMethod]
    public void Parse_GetThenNoGet_KeepsDataInTheBody()
    {
        CommandLineParseResult result = Parse(["-G", "--no-get", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("DataInQuery", false, CommandLineParseDiagnostics.Peek(result.Options)?.DataInQuery);
        Assert.IsFalse(result.Options.DataInQuery);
    }

    [TestMethod]
    public void Parse_NoGetThenGet_MovesDataIntoTheQuery()
    {
        CommandLineParseResult result = Parse(["--no-get", "--get", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("DataInQuery", true, CommandLineParseDiagnostics.Peek(result.Options)?.DataInQuery);
        Assert.IsTrue(result.Options.DataInQuery);
    }

    [TestMethod]
    public void Parse_NoGetWithAttachedValue_IgnoresTheValue()
    {
        CommandLineParseResult result = Parse(["-G", "--no-get=x", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Diagnostics.Assert("DataInQuery", false, CommandLineParseDiagnostics.Peek(result.Options)?.DataInQuery);
        Assert.IsFalse(result.Options.DataInQuery);
    }

    [TestMethod]
    public void Parse_NoDataAscii_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-data-ascii", Url]), "curl: option --no-data-ascii: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoDataAsciiWithAttachedValue_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-data-ascii=x", Url]), "curl: option --no-data-ascii=x: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoDataBinary_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-data-binary", Url]), "curl: option --no-data-binary: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoDataBinaryWithAttachedValue_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-data-binary=x", Url]), "curl: option --no-data-binary=x: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoDataRaw_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-data-raw", Url]), "curl: option --no-data-raw: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoDataRawWithAttachedValue_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-data-raw=x", Url]), "curl: option --no-data-raw=x: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoDataUrlencode_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-data-urlencode", Url]), "curl: option --no-data-urlencode: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoDataUrlencodeWithAttachedValue_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-data-urlencode=x", Url]), "curl: option --no-data-urlencode=x: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoJson_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-json", Url]), "curl: option --no-json: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoJsonWithAttachedValue_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-json=x", Url]), "curl: option --no-json=x: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoUrlQuery_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-url-query", Url]), "curl: option --no-url-query: " + CannotBeReversed);

    [TestMethod]
    public void Parse_NoUrlQueryWithAttachedValue_IsRefusedAsNotReversible() =>
        AssertRefused(Parse(["--no-url-query=x", Url]), "curl: option --no-url-query=x: " + CannotBeReversed);

    [TestMethod]
    [DataRow("--no-bogus")]
    [DataRow("--no-")]
    [DataRow("--no-no-silent")]
    [DataRow("--no-Silent")]
    public void Parse_NoPrefixWithoutARow_IsUnknown(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        AssertRefused(result, $"curl: option {argument}: is unknown");
    }

    [TestMethod]
    public void Parse_NoSilentAlone_IsRefusedForNoUrl()
    {
        CommandLineParseResult result = Parse(["--no-silent"]);

        AssertRefused(result, "curl: (2) no URL specified");
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertRefused(CommandLineParseResult result, string expectedFirstLine)
    {
        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, CommandLineParseDiagnostics.Peek(result.Refusal)?.ExitCode);
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach([expectedFirstLine, CommandLineRefusal.TryHelpLine]),
            CommandLineParseDiagnostics.QuoteEach(CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }
}
