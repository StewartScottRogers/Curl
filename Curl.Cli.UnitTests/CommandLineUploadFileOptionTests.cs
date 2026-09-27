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
    [TestMethod]
    [DataRow("-T")]
    [DataRow("--upload-file")]
    public void Parse_UploadFile_RecordsTheFile(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, "a", "http://h/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "a" }, result.Options.UploadFiles.ToArray());
    }

    [TestMethod]
    public void Parse_TwoUploadFilesAndTwoUrls_PairsThemInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-T", "a", "-T", "b", "http://h/1/", "http://h/2/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Options.UploadFiles.ToArray());
        CollectionAssert.AreEqual(new[] { "http://h/1/", "http://h/2/" }, result.Options.Urls.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyUploadFile_IsKeptInItsPlace()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-T", string.Empty, "-T", "a", "http://h/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { string.Empty, "a" }, result.Options.UploadFiles.ToArray());
    }

    [TestMethod]
    public void Parse_UploadFileThatLooksLikeAFlag_WarnsAndKeepsIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-T", "-o", "http://h/"]);

        Assert.IsTrue(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { "-o" }, result.Options.UploadFiles.ToArray());
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-o' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoUploadFile_HasNone()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["http://h/"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.Options.UploadFiles);
    }
}
