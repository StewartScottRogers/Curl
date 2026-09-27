namespace Curl.Cli;

/// <summary>
/// Pins <c>-G</c> / <c>--get</c> and <c>--url-query</c>, from the parsed options to the URL
/// <see cref="QueryUrl.Append"/> builds. Measured with the local curl 8.21.0 on 2026-09-26 against a
/// loopback server, reading the request line (<c>curl -sS &lt;url&gt; &lt;arguments&gt;</c> in Git
/// Bash, <c>f.txt</c> holding <c>a b</c>, CR LF, <c>c</c>, LF):
/// <c>/p -G -d a</c> requests <c>/p?a</c>; <c>/p -G</c> requests <c>/p</c>; <c>/p? -G -d a</c> requests
/// <c>/p?a</c>; <c>/p#frag -G -d a</c> requests <c>/p?a</c>; <c>/p?x=1 -G -d a</c> requests
/// <c>/p?x=1&amp;a</c>; <c>/p -G -d ''</c> requests <c>/p</c>; <c>/p?x -G -d ''</c> requests
/// <c>/p?x&amp;</c>; <c>/p -G --data-binary a --data-raw b</c> requests <c>/p?a&amp;b</c>;
/// <c>/p -d a -G --no-get</c> is a POST of <c>a</c>; <c>/p -d a --no-get -G</c> requests <c>/p?a</c>;
/// <c>/p?x=1 --url-query a</c> requests <c>/p?x=1&amp;a</c>; <c>/p --url-query '' --url-query n@f.txt</c>
/// requests <c>/p?&amp;n=a+b%0D%0Ac%0A</c>; <c>/p --url-query ''</c> requests <c>/p</c>;
/// <c>/p?x --url-query ''</c> requests <c>/p?x&amp;</c>; <c>/p --url-query '+a=b%20c' --url-query 'n=x y'
/// --url-query '=z w' --url-query k</c> requests <c>/p?a=b%20c&amp;n=x+y&amp;z+w&amp;k</c>;
/// <c>/p --url-query +@missing</c> requests <c>/p?@missing</c>; <c>/p --url-query a -G -d b</c> requests
/// <c>/p?b</c>; <c>/p -G --url-query b</c> requests <c>/p?b</c>; <c>/p -G -d '' --url-query b</c>
/// requests <c>/p</c>; <c>/p?x#f --url-query b</c> requests <c>/p?x&amp;b</c>;
/// <c>--url-query n@missing</c> exits 26 as <c>-d @missing</c> does.
/// </summary>
[TestClass]
public sealed class QueryUrlTests
{
    private const string Url = "http://127.0.0.1:18188/p";

    [TestMethod]
    [DataRow(Url, new[] { "-G", "-d", "a" }, Url + "?a")]
    [DataRow(Url, new[] { "-G" }, Url)]
    [DataRow(Url + "?", new[] { "-G", "-d", "a" }, Url + "?a")]
    [DataRow(Url + "#frag", new[] { "-G", "-d", "a" }, Url + "?a#frag")]
    [DataRow(Url + "?x=1", new[] { "-G", "-d", "a" }, Url + "?x=1&a")]
    [DataRow(Url, new[] { "-G", "-d", "" }, Url)]
    [DataRow(Url + "?x", new[] { "-G", "-d", "" }, Url + "?x&")]
    [DataRow(Url, new[] { "--get", "--data-binary", "a", "--data-raw", "b" }, Url + "?a&b")]
    [DataRow(Url, new[] { "-d", "a", "--no-get", "-G" }, Url + "?a")]
    [DataRow(Url, new[] { "-d", "a", "-G", "--no-get" }, Url)]
    [DataRow(Url + "?x=1", new[] { "--url-query", "a" }, Url + "?x=1&a")]
    [DataRow(Url, new[] { "--url-query", "" }, Url)]
    [DataRow(Url + "?x", new[] { "--url-query", "" }, Url + "?x&")]
    [DataRow(Url, new[] { "--url-query", "+a=b%20c", "--url-query", "n=x y", "--url-query", "=z w", "--url-query", "k" }, Url + "?a=b%20c&n=x+y&z+w&k")]
    [DataRow(Url, new[] { "--url-query", "+@missing" }, Url + "?@missing")]
    [DataRow(Url, new[] { "--url-query", "+", "--url-query", "q" }, Url + "?&q")]
    [DataRow(Url, new[] { "--url-query", "a", "-G", "-d", "b" }, Url + "?b")]
    [DataRow(Url, new[] { "-G", "--url-query", "b" }, Url + "?b")]
    [DataRow(Url, new[] { "-G", "-d", "", "--url-query", "b" }, Url)]
    [DataRow(Url + "?x#f", new[] { "--url-query", "b" }, Url + "?x&b#f")]
    [DataRow(Url, new[] { "-d", "a" }, Url)]
    public void Append_ParsedCommandLine_BuildsTheUrlCurlRequests(string url, string[] arguments, string expectedUrl)
    {
        CommandLineParseResult result = Parse([.. arguments, url], new RecordingDataFileReader());

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(expectedUrl, QueryUrl.Append(url, result.Options));
    }

    [TestMethod]
    public void Append_UrlQueryAtFile_EncodesTheFileAfterAnEmptyPiece()
    {
        RecordingDataFileReader reader = new() { Files = { ["f.txt"] = "a b\r\nc\n"u8.ToArray() } };

        CommandLineParseResult result = Parse(["--url-query", "", "--url-query", "n@f.txt", Url], reader);

        Assert.AreEqual("&n=a+b%0D%0Ac%0A", result.Options!.UrlQuery);
        Assert.AreEqual(Url + "?&n=a+b%0D%0Ac%0A", QueryUrl.Append(Url, result.Options));
    }

    [TestMethod]
    public void Parse_UrlQueryAtMissingFile_IsRefusedWithReadError()
    {
        CommandLineParseResult result = Parse(["--url-query", "n@missing", Url], new RecordingDataFileReader());

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[]
            {
                "curl: Failed to open missing",
                "curl: option --url-query: error encountered when reading a file",
                CommandLineRefusal.TryHelpLine,
            },
            result.Refusal!.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoUrlQuery_LeavesUrlQueryNull()
    {
        CommandLineParseResult result = Parse([Url], new RecordingDataFileReader());

        Assert.IsNull(result.Options!.UrlQuery);
        Assert.IsFalse(result.Options!.DataInQuery);
    }

    [TestMethod]
    public void Append_NullUrl_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => QueryUrl.Append(null!, new CommandLineOptions()));

        Assert.AreEqual("url", exception.ParamName);
    }

    [TestMethod]
    public void Append_NullOptions_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => QueryUrl.Append(Url, null!));

        Assert.AreEqual("options", exception.ParamName);
    }

    private static CommandLineParseResult Parse(IReadOnlyList<string> arguments, IDataFileReader reader) =>
        CommandLineParser.Parse(arguments, _ => true, new UnexpectedPasswordPrompt(), reader);

    private sealed class UnexpectedPasswordPrompt : IPasswordPrompt
    {
        public string ReadPassword(string prompt) => throw new AssertFailedException("No password prompt was expected.");
    }
}
