namespace Curl.Cli;

/// <summary>
/// Pins how <c>--out-null</c> pairs with URLs: as one more output option, the Nth URL taking the
/// Nth <c>-o</c>, <c>-O</c> or <c>--out-null</c> in command-line order (ADR-0029). Measured with the
/// local curl 8.21.0 on 2026-09-28 (BL-495 Notes): <c>--out-null -o b.txt u1 u2</c> saves only
/// <c>u2</c>; <c>-o c.txt --out-null u1 u2</c> saves only <c>u1</c>; <c>u --out-null</c> discards;
/// <c>--no-out-null u</c> discards too; <c>--remote-name-all --out-null u</c> saves nothing; and
/// <c>--out-null u --out-null</c> warns <c>Warning: Got more output options than URLs</c>.
/// </summary>
[TestClass]
public sealed class CommandLineOutNullOptionTests
{
    private const string Url = "http://127.0.0.1:1/a";

    private const string OtherUrl = "http://127.0.0.1:1/b";

    [TestMethod]
    [DataRow("--out-null")]
    [DataRow("--no-out-null")]
    public void Parse_OutNull_DiscardsItsUrlsBody(string spelledOption)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelledOption, Url]);

        Assert.IsTrue(result.IsAccepted);
        UrlOutput output = result.Options.UrlOutputs.Single();
        Assert.AreEqual(Url, output.Url);
        Assert.IsTrue(output.DiscardsBody);
        Assert.IsNull(output.FileName);
        Assert.IsFalse(output.UsesRemoteName);
        Assert.IsEmpty(result.WarningLinesAfterTransfers);
    }

    [TestMethod]
    public void Parse_NoOutputOption_KeepsTheBody()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.UrlOutputs.Single().DiscardsBody);
    }

    [TestMethod]
    public void Parse_OutNullAfterItsUrl_StillPairsWithIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, "--out-null"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.UrlOutputs.Single().DiscardsBody);
    }

    [TestMethod]
    public void Parse_OutNullThenOutput_PairsEachWithItsUrlInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--out-null", "-o", "b.txt", Url, OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Options.UrlOutputs);
        Assert.AreEqual(Url, result.Options.UrlOutputs[0].Url);
        Assert.IsTrue(result.Options.UrlOutputs[0].DiscardsBody);
        Assert.IsNull(result.Options.UrlOutputs[0].FileName);
        Assert.AreEqual(OtherUrl, result.Options.UrlOutputs[1].Url);
        Assert.IsFalse(result.Options.UrlOutputs[1].DiscardsBody);
        Assert.AreEqual("b.txt", result.Options.UrlOutputs[1].FileName);
        CollectionAssert.AreEqual(new string?[] { null, "b.txt" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_OutputThenOutNull_PairsEachWithItsUrlInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-o", "c.txt", "--out-null", Url, OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("c.txt", result.Options.UrlOutputs[0].FileName);
        Assert.IsFalse(result.Options.UrlOutputs[0].DiscardsBody);
        Assert.IsTrue(result.Options.UrlOutputs[1].DiscardsBody);
        CollectionAssert.AreEqual(new string?[] { "c.txt" }, result.Options.OutputFiles.ToArray());
    }

    [TestMethod]
    public void Parse_OutNullThenRemoteName_PairsEachWithItsUrlInOrder()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--out-null", "-O", Url, OtherUrl]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.UrlOutputs[0].DiscardsBody);
        Assert.IsFalse(result.Options.UrlOutputs[0].UsesRemoteName);
        Assert.IsFalse(result.Options.UrlOutputs[1].DiscardsBody);
        Assert.IsTrue(result.Options.UrlOutputs[1].UsesRemoteName);
    }

    [TestMethod]
    public void Parse_OutNullUnderRemoteNameAll_DoesNotUseTheRemoteName()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--remote-name-all", "--out-null", Url]);

        Assert.IsTrue(result.IsAccepted);
        UrlOutput output = result.Options.UrlOutputs.Single();
        Assert.IsTrue(output.DiscardsBody);
        Assert.IsFalse(output.UsesRemoteName);
    }

    [TestMethod]
    public void Parse_MoreOutNullsThanUrls_WarnsAfterTheTransfers()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--out-null", Url, "--out-null"]);

        Assert.IsTrue(result.IsAccepted);
        Assert.HasCount(2, result.Options.UrlOutputs);
        Assert.IsNull(result.Options.UrlOutputs[1].Url);
        CollectionAssert.AreEqual(new[] { CommandLineWarning.MoreOutputOptionsThanUrls }, result.WarningLinesAfterTransfers.ToArray());
    }
}
