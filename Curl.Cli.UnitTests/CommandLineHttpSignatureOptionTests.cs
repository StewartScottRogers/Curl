using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--httpsig-algo</c>, <c>--httpsig-key</c>, <c>--httpsig-keyid</c> and <c>--httpsig-headers</c> as
/// curl 8.22.0 parses them (<c>src/tool_getparam.c</c> at <c>curl-8_22_0</c>; no reference build has the
/// options, ADR-0485): each is a string kept verbatim, the last one winning, an empty value refused as
/// blank, and <c>--httpsig-key</c>, an <c>ARG_FILE</c> option, warns about a value that looks like a flag.
/// Nothing is checked or read here: curl does that when it sets up an HTTP transfer (BL-2182).
/// </summary>
[TestClass]
public sealed class CommandLineHttpSignatureOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    /// <summary>Gets or sets the MSTest context the diagnostics are written to.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoHttpSignatureOption_LeavesEveryValueNull()
    {
        CommandLineParseResult result = Parse([Url]);

        string?[] values = ValuesOf(result.Options!);
        Diagnostics.Assert("values", "null null null null", string.Join(' ', values.Select(value => value ?? "null")));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new string?[] { null, null, null, null }, values);
    }

    [TestMethod]
    public void Parse_EveryHttpSignatureOption_RecordsItsValueVerbatim()
    {
        CommandLineParseResult result = Parse(["--httpsig-algo", "HMAC-sha256", "--httpsig-key", "@keys/k.hex", "--httpsig-keyid", "my key", "--httpsig-headers", "method  Content-Type:", Url]);

        string?[] values = ValuesOf(result.Options!);
        Diagnostics.Assert("values", "HMAC-sha256|@keys/k.hex|my key|method  Content-Type:", string.Join('|', values));
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "HMAC-sha256", "@keys/k.hex", "my key", "method  Content-Type:" }, values);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--httpsig-algo", 0)]
    [DataRow("--httpsig-key", 1)]
    [DataRow("--httpsig-keyid", 2)]
    [DataRow("--httpsig-headers", 3)]
    public void Parse_HttpSignatureOptionGivenTwice_KeepsTheLastValue(string spelledOption, int index)
    {
        CommandLineParseResult result = Parse([spelledOption, "first", spelledOption, "second", Url]);

        string? value = ValuesOf(result.Options!)[index];
        Diagnostics.Assert(spelledOption, "second", value);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("second", value);
    }

    [TestMethod]
    [DataRow("--httpsig-algo")]
    [DataRow("--httpsig-key")]
    [DataRow("--httpsig-keyid")]
    [DataRow("--httpsig-headers")]
    public void Parse_HttpSignatureOptionGivenEmptyValue_RefusesAsBlank(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "", Url]);

        string expectedFirstLine = $"curl: option {spelledOption}: blank argument where content is expected";
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        Diagnostics.Diff(
            "stderr",
            string.Join('\n', expectedFirstLine, CommandLineRefusal.TryHelpLine),
            string.Join('\n', result.Refusal?.StandardErrorLines ?? []));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { expectedFirstLine, CommandLineRefusal.TryHelpLine }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_HttpSignatureKeyThatLooksLikeAFlag_AcceptsWithFileNameWarning()
    {
        CommandLineParseResult result = Parse(["--httpsig-key", "-x", Url]);

        Diagnostics.Assert(
            "warning lines",
            CommandLineParseDiagnostics.QuoteEach(["Warning: The filename argument '-x' looks like a flag."]),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("-x", result.Options.HttpSignatureKey);
        CollectionAssert.AreEqual(new[] { "Warning: The filename argument '-x' looks like a flag." }, result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow("--no-httpsig-algo")]
    [DataRow("--no-httpsig-key")]
    [DataRow("--no-httpsig-keyid")]
    [DataRow("--no-httpsig-headers")]
    public void Parse_NoPrefixedHttpSignatureOption_IsRefused(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, Url]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Assert.IsFalse(result.IsAccepted);
    }

    private static string?[] ValuesOf(CommandLineOptions options) =>
        [options.HttpSignatureAlgorithm, options.HttpSignatureKey, options.HttpSignatureKeyId, options.HttpSignatureHeaders];

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = OpenSslBuildParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
