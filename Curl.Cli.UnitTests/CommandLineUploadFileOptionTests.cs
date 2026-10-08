using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>-T</c> / <c>--upload-file</c>, measured against curl 8.21.0 on
/// 2026-09-27 (BL-030 Notes): every value is kept in order and pairs with the URL at the same
/// position wherever it was given, an empty value keeps its place, and a value that looks like a
/// flag is accepted with curl's warning.
/// </summary>
[TestClass]
public sealed class CommandLineUploadFileOptionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-T")]
    [DataRow("--upload-file")]
    public void Parse_UploadFile_RecordsTheFile(string spelledOption)
    {
        CommandLineParseResult result = Parse([spelledOption, "a", "http://h/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertList("upload files", ["a"], Recorded(result)?.UploadFiles);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "a" }, result.Options.UploadFiles.ToArray());
    }

    [TestMethod]
    public void Parse_TwoUploadFilesAndTwoUrls_PairsThemInOrder()
    {
        CommandLineParseResult result = Parse(["-T", "a", "-T", "b", "http://h/1/", "http://h/2/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertList("upload files", ["a", "b"], Recorded(result)?.UploadFiles);
        AssertList("URLs", ["http://h/1/", "http://h/2/"], Recorded(result)?.Urls);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Options.UploadFiles.ToArray());
        CollectionAssert.AreEqual(new[] { "http://h/1/", "http://h/2/" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyUploadFile_IsKeptInItsPlace()
    {
        CommandLineParseResult result = Parse(["-T", string.Empty, "-T", "a", "http://h/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertList("upload files", [string.Empty, "a"], Recorded(result)?.UploadFiles);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { string.Empty, "a" }, result.Options.UploadFiles.ToArray());
    }

    [TestMethod]
    public void Parse_UploadFileThatLooksLikeAFlag_WarnsAndKeepsIt()
    {
        CommandLineParseResult result = Parse(["-T", "-o", "http://h/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertList("upload files", ["-o"], Recorded(result)?.UploadFiles);
        AssertList("warning lines", ["Warning: The filename argument '-o' looks like a flag."], result.WarningLines);
        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-o" }, result.Options.UploadFiles.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-o' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoUploadFile_HasNone()
    {
        CommandLineParseResult result = Parse(["http://h/"]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        AssertList("upload files", [], Recorded(result)?.UploadFiles);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.Options.UploadFiles);
    }

    /// <summary>
    /// Returns the parsed options, or null for a refusal, for diagnostic lines written before the test asserts
    /// acceptance, without making the compiler treat <see cref="CommandLineParseResult.Options"/> as possibly null.
    /// </summary>
    private static CommandLineOptions? Recorded(CommandLineParseResult result) => result.Options;

    /// <summary>Parses <paramref name="arguments"/>, writing them, the outcome, the upload files and the URLs as diagnostics.</summary>
    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("upload files", CommandLineParseDiagnostics.QuoteEach(result.Options.UploadFiles));
            Diagnostics.Act("URLs", CommandLineParseDiagnostics.QuoteEach(result.Options.Urls));
        }

        return result;
    }

    private void AssertList(string label, IEnumerable<string> expected, IEnumerable<string>? actual) =>
        Diagnostics.Assert(label, CommandLineParseDiagnostics.QuoteEach(expected), actual is null ? "none" : CommandLineParseDiagnostics.QuoteEach(actual));
}
