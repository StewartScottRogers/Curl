using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCurlOnlyHelpLines"/>: it removes Curl's own <c>--ai-help</c> lines from
/// <c>--help</c> output (BL-2985) and nothing else.
/// </summary>
[TestClass]
public sealed class UpstreamCurlOnlyHelpLinesTests
{
    [TestMethod]
    [DataRow("Use \"--ai-help\" for Markdown help written for AI agents (Curl only)")]
    [DataRow("     --ai-help <category>                      Markdown help for AI agents (Curl only)")]
    [DataRow("     --ai-help <category>  Markdown help for AI agents (Curl only)")]
    public void IsCurlOnly_AiHelpLine_IsTrue(string line)
    {
        bool curlOnly = UpstreamCurlOnlyHelpLines.IsCurlOnly(line);

        Assert.IsTrue(curlOnly);
    }

    [TestMethod]
    [DataRow("Use \"--help [option]\" to view documentation for a given option")]
    [DataRow("     --ai-help <category>  Something else")]
    [DataRow("     --alt-svc <filename>  Markdown help for AI agents (Curl only)")]
    [DataRow("")]
    public void IsCurlOnly_AnyOtherLine_IsFalse(string line)
    {
        bool curlOnly = UpstreamCurlOnlyHelpLines.IsCurlOnly(line);

        Assert.IsFalse(curlOnly);
    }

    [TestMethod]
    public void Remove_HelpOutput_DropsOnlyTheCurlOnlyLinesAndKeepsLineEnds()
    {
        string output = " -a  A\r\n     --ai-help <category>  Markdown help for AI agents (Curl only)\r\n -b  B\n"
            + "Use \"--ai-help\" for Markdown help written for AI agents (Curl only)\n";

        string kept = Encoding.Latin1.GetString(UpstreamCurlOnlyHelpLines.Remove(Encoding.Latin1.GetBytes(output)));

        Assert.AreEqual(" -a  A\r\n -b  B\n", kept);
    }
}
