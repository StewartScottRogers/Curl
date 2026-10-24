using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1276Script"/>: upstream's <c>tests/test1276.pl</c> over the vendored
/// <c>lib/optiontable.pl</c>, <c>include/curl/curl.h</c> and <c>lib/easyoptions.c</c>, how it builds the table from the
/// header, and every kind of difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1276ScriptTests
{
    private static readonly string VendoredRoot = Path.Combine(AppContext.BaseDirectory, "CurlTestCases", "Original");

    private string root = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1276-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "include", "curl"));
        Directory.CreateDirectory(Path.Combine(root, "lib"));
        File.Copy(Path.Combine(VendoredRoot, "lib", "optiontable.pl"), Path.Combine(root, "lib", "optiontable.pl"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_TheVendoredFiles_WritesNothing()
    {
        UpstreamTest1276Script.Result result = UpstreamTest1276Script.Check(VendoredRoot);

        Assert.AreEqual(new UpstreamTest1276Script.Result("", "", 0), result);
    }

    [TestMethod]
    public void Check_AVendoredHeaderWithOneOptionChanged_ReportsTheDifferingLines()
    {
        string brokenHeader = File.ReadAllText(Path.Combine(VendoredRoot, "include", "curl", "curl.h"), Encoding.Latin1).Replace("CURLOPT_ABSTRACT_UNIX_SOCKET", "CURLOPT_ABSTRACT_UNIX_SOCKETX", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(root, "include", "curl", "curl.h"), brokenHeader, Encoding.Latin1);
        File.Copy(Path.Combine(VendoredRoot, "lib", "easyoptions.c"), Path.Combine(root, "lib", "easyoptions.c"));

        UpstreamTest1276Script.Result result = UpstreamTest1276Script.Check(root);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.StartsWith(result.Output, "easyoptions.c need to be regenerated!\neasyoptions.c is 394 lines\ngenerated file is ");
        StringAssert.Contains(result.Output, "Gen:  ");
        StringAssert.Contains(result.Output, "CURLOPT_ABSTRACT_UNIX_SOCKETX");
    }

    [TestMethod]
    public void Check_EveryKindOfLine_BuildsTheTableAsOptionTableDoes()
    {
        Write(
            "include/curl/curl.h",
            "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 2)\n"
            + "CURLOPT(CURLOPT_OBSOLETE5, CURLOPTTYPE_LONG, 5)\n"
            + "CURLOPTDEPRECATED(CURLOPT_OLD, CURLOPTTYPE_CBPOINT, 7,\n"
            + "    8,\n"
            + "    7.1, \"x\"),\n"
            + "#define CURLOPT_ALIAS CURLOPT_URL\n"
            + "#define CURLOPT_OBSOLETEALIAS CURLOPT_URL\n"
            + "#define CURLOPT_ALIAS2 CURLOPT_OBSOLETE5\n"
            + "CURLOPT(NOTPREFIXED, CURLOPTTYPE_LONG, 8)\n"
            + "CURLOPT(CURLOPT_AVERYLONGOPTIONNAMETHATMAKESTHEENTRYLINELONGERTHANEIGHTYCHARACTERS, CURLOPTTYPE_OBJECTPOINT, 9)\n");
        WriteEasyOptions(
            "  { \"\", NOTPREFIXED, CURLOT_LONG, 0 },\n"
            + "  { \"ALIAS\", CURLOPT_URL, CURLOT_STRING, CURLOT_FLAG_ALIAS },\n"
            + "  { \"AVERYLONGOPTIONNAMETHATMAKESTHEENTRYLINELONGERTHANEIGHTYCHARACTERS\", CURLOPT_AVERYLONGOPTIONNAMETHATMAKESTHEENTRYLINELONGERTHANEIGHTYCHARACTERS,\n"
            + "    CURLOT_OBJECT, 0 },\n"
            + "  { \"OLD\", CURLOPT_OLD, CURLOT_CBPTR, 0 },\n"
            + "  { \"URL\", CURLOPT_URL, CURLOT_STRING, 0 },\n",
            9);

        Assert.AreEqual(new UpstreamTest1276Script.Result("", "", 0), UpstreamTest1276Script.Check(root));
    }

    [TestMethod]
    public void Check_AnObsoleteDeprecatedOption_KeepsAppendingLinesUntilTheNextEndAsTheScriptDoes()
    {
        Write(
            "include/curl/curl.h",
            "CURLOPTDEPRECATED(CURLOPT_OBSOLETE9, CURLOPTTYPE_LONG, 9, \n"
            + "  7.1, \"x\"),\n"
            + "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 2)\n");
        WriteEasyOptions("  { \"URL\", CURLOPT_URL, CURLOT_STRING, 0 },\n", 2);

        Assert.AreEqual(new UpstreamTest1276Script.Result("", "", 0), UpstreamTest1276Script.Check(root));
    }

    [TestMethod]
    public void Check_ADeprecatedOptionWithTooFewFieldsAndAnUnfinishedLastLine_ReadsMissingFieldsAsEmpty()
    {
        Write("include/curl/curl.h", "CURLOPTDEPRECATED(CURLOPT_C\n x),\nCURLOPTDEPRECATED(CURLOPT_B,\ntail");
        WriteEasyOptions("  { \"C x\", CURLOPT_C x, CURLOT_, 0 },\n", 0);

        Assert.AreEqual(new UpstreamTest1276Script.Result("", "", 0), UpstreamTest1276Script.Check(root));
    }

    [TestMethod]
    public void Check_ANumberLowerThanTheOneBefore_ReportsItOnStandardErrorAndExitsZeroOverTheShortGeneratedFile()
    {
        Write("include/curl/curl.h", "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 5)\nCURLOPT(CURLOPT_B, CURLOPTTYPE_LONG, 3)\n");
        File.Copy(Path.Combine(VendoredRoot, "lib", "easyoptions.c"), Path.Combine(root, "lib", "easyoptions.c"));

        UpstreamTest1276Script.Result result = UpstreamTest1276Script.Check(root);

        Assert.AreEqual("ERROR: CURLOPT_B has bad number: 3 < 5\n", result.Error);
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.Output, "easyoptions.c is 394 lines\ngenerated file is 32 lines\n");
    }

    [TestMethod]
    public void Check_ADeprecatedNumberLowerThanTheOneBefore_ReportsIt()
    {
        Write("include/curl/curl.h", "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 5)\nCURLOPTDEPRECATED(CURLOPT_B, CURLOPTTYPE_LONG, 3, \n 7.1),\n");
        File.Copy(Path.Combine(VendoredRoot, "lib", "easyoptions.c"), Path.Combine(root, "lib", "easyoptions.c"));

        Assert.AreEqual("ERROR: CURLOPT_B has bad number: 3 < 5\n", UpstreamTest1276Script.Check(root).Error);
    }

    [TestMethod]
    public void Check_ManyDifferingLines_ShowsElevenAndShowsOddCharactersAsHex()
    {
        Write("include/curl/curl.h", "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 2)\n");
        File.WriteAllText(Path.Combine(root, "lib", "easyoptions.c"), "\té\nx\n", Encoding.Latin1);

        UpstreamTest1276Script.Result result = UpstreamTest1276Script.Check(root);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual(11, result.Output.Split("Gen:  ").Length - 1);
        StringAssert.StartsWith(result.Output, "easyoptions.c need to be regenerated!\neasyoptions.c is 2 lines\ngenerated file is ");
        StringAssert.Contains(result.Output, "File: 1:%09%e9\nGen:  1:/***");
    }

    [TestMethod]
    public void Check_OnlyLineEndsOrExtraTrailingLinesDiffer_WritesTheSummaryAndExitsZero()
    {
        Write("include/curl/curl.h", "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 2)\n");
        WriteEasyOptions("  { \"URL\", CURLOPT_URL, CURLOT_STRING, 0 },\n", 2);
        string path = Path.Combine(root, "lib", "easyoptions.c");
        string generatedLines = File.ReadAllText(path);
        File.WriteAllText(path, generatedLines.Replace("\n", "\r\n", StringComparison.Ordinal) + "extra\r\n");

        UpstreamTest1276Script.Result result = UpstreamTest1276Script.Check(root);

        Assert.AreEqual(0, result.ExitCode);
        int lines = generatedLines.Count(character => character == '\n');
        Assert.AreEqual($"easyoptions.c need to be regenerated!\neasyoptions.c is {lines + 1} lines\ngenerated file is {lines} lines\n", result.Output);
    }

    [TestMethod]
    public async Task RunAsync_TheSourceRoot_WritesTheResultToStandardOutputAndError()
    {
        Write("include/curl/curl.h", "CURLOPT(CURLOPT_URL, CURLOPTTYPE_STRINGPOINT, 5)\nCURLOPT(CURLOPT_B, CURLOPTTYPE_LONG, 3)\n");
        File.WriteAllText(Path.Combine(root, "lib", "easyoptions.c"), "");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1276Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("ERROR: CURLOPT_B has bad number: 3 < 5\n", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.IsGreaterThan(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1276Script.RunAsync(null!, _ => Task.FromResult(0)));

    // easyoptions.c as optiontable.pl would print it: the vendored file's header and footer around the given entries.
    private void WriteEasyOptions(string entries, int lastNumber)
    {
        string vendored = File.ReadAllText(Path.Combine(VendoredRoot, "lib", "easyoptions.c"), Encoding.Latin1);
        int tableStart = vendored.IndexOf("= {\n", StringComparison.Ordinal) + "= {\n".Length;
        int footerStart = vendored.IndexOf("  { NULL,", StringComparison.Ordinal);
        string footer = vendored[footerStart..].Replace("(332 + 1)", $"({lastNumber} + 1)", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(root, "lib", "easyoptions.c"), vendored[..tableStart] + entries + footer, Encoding.Latin1);
    }

    private void Write(string relativePath, string text) => File.WriteAllText(Path.Combine(root, relativePath), text, Encoding.Latin1);

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
