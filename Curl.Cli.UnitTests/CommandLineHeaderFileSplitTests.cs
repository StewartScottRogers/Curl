using System.Text;

namespace Curl.Cli;

/// <summary>Pins that a header file is split on LF only, with at most one trailing CR trimmed per line (BL-5038).</summary>
[TestClass]
public sealed class CommandLineHeaderFileSplitTests
{
    private const string Url = "http://127.0.0.1/a";

    [TestMethod]
    public void Parse_HeaderFileWithLoneCr_KeepsTheCrInsideOneHeader()
    {
        CommandLineOptions options = Parse(["-H", "@h.txt", Url], "X-A: a\rX-B: b\n");

        Assert.AreEqual("X-A: a\rX-B: b", options.Headers.Single());
    }

    [TestMethod]
    public void Parse_HeaderFileWithCrLfAndBlankLines_YieldsTheTwoHeaders()
    {
        CommandLineOptions options = Parse(["-H", "@h.txt", Url], "A: 1\r\n\r\nB: 2\r\n\n");

        CollectionAssert.AreEqual(new[] { "A: 1", "B: 2" }, options.Headers.ToArray());
    }

    [TestMethod]
    public void Parse_FormHeaderFileWithLoneCr_KeepsTheCrInsideOneHeader()
    {
        CommandLineOptions options = Parse(["-F", "a=b;headers=@h.txt", Url], "X-A: a\rX-B: b\n");

        Assert.AreEqual("X-A: a\rX-B: b", options.FormParts.Single().Headers.Single());
    }

    [TestMethod]
    public void Parse_FormHeaderFileWithCrLfAndBlankLines_YieldsTheTwoHeaders()
    {
        CommandLineOptions options = Parse(["-F", "a=b;headers=@h.txt", Url], "A: 1\r\n\r\nB: 2\r\n");

        CollectionAssert.AreEqual(new[] { "A: 1", "B: 2" }, options.FormParts.Single().Headers.ToArray());
    }

    private static CommandLineOptions Parse(string[] arguments, string contents)
    {
        RecordingDataFileReader reader = new();
        reader.Files["h.txt"] = Encoding.ASCII.GetBytes(contents);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, _ => true, new EmptyPasswordPrompt(), reader, false);
        Assert.IsNull(result.Refusal);
        return result.Options!;
    }
}
