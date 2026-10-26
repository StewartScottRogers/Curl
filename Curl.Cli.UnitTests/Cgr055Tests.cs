namespace Curl.Cli;

/// <summary>
/// Pins CGR-055 (BL-3584): curl 8.22.0 appends a <c>-G</c> / <c>--url-query</c> query by re-parsing the
/// URL without <c>CURLU_PATH_AS_IS</c>, so <c>--path-as-is</c> keeps the dot segments only when there is
/// no query to append. Measured on Windows with curl 8.22.0 on 2026-10-26: <c>-G -d a=b --path-as-is
/// http://127.0.0.1:20601/x/../y</c>, <c>--url-query a=b</c> and <c>-G -d ''</c> request <c>/y</c>; without
/// a query <c>/x/../y</c> is requested.
/// </summary>
[TestClass]
public sealed class Cgr055Tests
{
    private const string Url = "http://127.0.0.1:20601/x/../y";

    [TestMethod]
    [DataRow("-G", "-d", "a=b")]
    [DataRow("-G", "-d", "")]
    [DataRow("--url-query", "a=b", "-s")]
    [DataRow("-G", "--url-query", "a=b")]
    public void KeepsPathAsIs_QueryToAppend_IsFalse(string first, string second, string third)
    {
        CommandLineOptions options = Parse(["--path-as-is", first, second, third, Url]);

        Assert.IsFalse(QueryUrl.KeepsPathAsIs(options));
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("-G")]
    [DataRow("-d")]
    public void KeepsPathAsIs_NoQueryToAppend_IsTrue(string other)
    {
        CommandLineOptions options = Parse(other == "-d" ? ["--path-as-is", "-d", "a=b", Url] : ["--path-as-is", other, Url]);

        Assert.IsTrue(QueryUrl.KeepsPathAsIs(options));
    }

    [TestMethod]
    public void KeepsPathAsIs_WithoutPathAsIs_IsFalse()
    {
        CommandLineOptions options = Parse([Url]);

        Assert.IsFalse(QueryUrl.KeepsPathAsIs(options));
    }

    [TestMethod]
    public void KeepsPathAsIs_NullOptions_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => QueryUrl.KeepsPathAsIs(null!));

        Assert.AreEqual("options", exception.ParamName);
    }

    private static CommandLineOptions Parse(IReadOnlyList<string> arguments) =>
        CommandLineParser.Parse(arguments, _ => true, new NoPasswordPrompt(), new RecordingDataFileReader()).Options!;

    private sealed class NoPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
