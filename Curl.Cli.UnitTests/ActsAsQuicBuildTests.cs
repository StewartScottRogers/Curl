namespace Curl.Cli;

/// <summary>Tests that <see cref="CommandLineOptions.ActsAsQuicBuild"/> reports the build a parse acted as.</summary>
[TestClass]
public sealed class ActsAsQuicBuildTests
{
    [TestMethod]
    public void ActsAsQuicBuild_ParsedAsQuicBuild_IsTrue()
    {
        CommandLineParseResult result = QuicBuildParser.Parse(["http://example.com/"]);

        Assert.AreEqual(true, result.Options?.ActsAsQuicBuild);
    }

    [TestMethod]
    public void ActsAsQuicBuild_ParsedAsDefaultBuild_IsFalse()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["http://example.com/"]);

        Assert.AreEqual(false, result.Options?.ActsAsQuicBuild);
    }
}
