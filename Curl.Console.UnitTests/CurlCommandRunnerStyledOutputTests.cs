using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins when the runner styles <c>-i</c> header lines, as curl 8.21.0's <c>tool_header_cb</c>
/// does (ADR-0246, BL-736): only for a transfer writing to standard output when standard output
/// is a terminal that renders bold, under <c>--styled-output</c> (the default), and for an
/// <c>http</c>, <c>https</c>, <c>rtsp</c> or <c>file</c> URL. The bytes are
/// <c>Curl.Output</c>'s <c>StyledHeaderLines</c>, measured with curl on Linux (BL-736 Notes).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerStyledOutputTests
{
    private const string Head = "HTTP/1.1 301 Moved Permanently\r\nLocation: next\r\n\r\n";

    private const string StyledHead =
        "HTTP/1.1 301 Moved Permanently\r\n"
        + "\e[1mLocation\e[0m: \e]8;;http://127.0.0.1:8099/d/next\e\\next\r\n\e]8;;\e\\\r\n";

    private readonly MemoryStream standardOutput = new();
    private readonly InMemoryFileSystem outputFiles = new();

    private string StandardOutputText => Encoding.Latin1.GetString(standardOutput.ToArray());

    [TestMethod]
    public async Task RunAsync_IncludeOnATerminal_StylesTheHeaderLines()
    {
        await RunAsync(["-s", "-i", "http://127.0.0.1:8099/d/a"], "http");

        Assert.AreEqual(StyledHead, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IncludeWithDumpHeaderToStandardOutput_StylesOnlyTheIncludedLines()
    {
        await RunAsync(["-s", "-i", "-D", "-", "http://127.0.0.1:8099/d/a"], "http");

        Assert.AreEqual(
            "HTTP/1.1 301 Moved Permanently\r\nHTTP/1.1 301 Moved Permanently\r\n"
            + "Location: next\r\n\e[1mLocation\e[0m: \e]8;;http://127.0.0.1:8099/d/next\e\\next\r\n\e]8;;\e\\"
            + "\r\n\r\n",
            StandardOutputText);
    }

    [TestMethod]
    [DataRow("https://127.0.0.1:8099/d/a", "https")]
    [DataRow("rtsp://127.0.0.1:8099/d/a", "rtsp")]
    [DataRow("file:///d/a", "file")]
    public async Task RunAsync_IncludeOnATerminalForAnotherStyledScheme_BoldsTheNames(string url, string scheme)
    {
        await RunAsync(["-s", "-i", url], scheme);

        Assert.StartsWith("HTTP/1.1 301 Moved Permanently\r\n\e[1mLocation\e[0m: ", StandardOutputText);
    }

    [TestMethod]
    [DataRow("http://u:p@127.0.0.1:8099/d/a?q=1", "http://u:p@127.0.0.1:8099/d/next")]
    [DataRow("http://u@127.0.0.1/d/a", "http://u@127.0.0.1/d/next")]
    public async Task RunAsync_IncludeOnATerminal_ResolvesTheLocationAgainstTheWholeTransferUrl(string url, string link)
    {
        await RunAsync(["-s", "-i", url], "http");

        StringAssert.Contains(StandardOutputText, "\e]8;;" + link + "\e\\next\r\n");
    }

    [TestMethod]
    public async Task RunAsync_IncludeOnWindowsTerminal_BoldsWithBoldOffAndLinksNothing()
    {
        await RunAsync(["-s", "-i", "http://127.0.0.1:8099/d/a"], "http", runsOnWindows: true);

        Assert.AreEqual("HTTP/1.1 301 Moved Permanently\r\n\e[1mLocation\e[22m: next\r\n\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IncludeOnATerminalInsideOldVte_LinksNothing()
    {
        await RunAsync(
            ["-s", "-i", "http://127.0.0.1:8099/d/a"],
            "http",
            readEnvironmentVariable: name => name == "VTE_VERSION" ? "4801" : null);

        Assert.AreEqual("HTTP/1.1 301 Moved Permanently\r\n\e[1mLocation\e[0m: next\r\n\r\n", StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_NoStyledOutput_WritesTheHeaderLinesAsTheyCome()
    {
        await RunAsync(["-s", "-i", "--no-styled-output", "http://127.0.0.1:8099/d/a"], "http");

        Assert.AreEqual(Head, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_StandardOutputIsNoTerminal_WritesTheHeaderLinesAsTheyCome()
    {
        await RunAsync(["-s", "-i", "http://127.0.0.1:8099/d/a"], "http", standardOutputIsTerminal: false);

        Assert.AreEqual(Head, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_TerminalRendersNoStyles_WritesTheHeaderLinesAsTheyCome()
    {
        await RunAsync(["-s", "-i", "http://127.0.0.1:8099/d/a"], "http", terminalRendersStyles: false);

        Assert.AreEqual(Head, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_UnstyledScheme_WritesTheHeaderLinesAsTheyCome()
    {
        await RunAsync(["-s", "-i", "dict://127.0.0.1:8099/d/a"], "dict");

        Assert.AreEqual(Head, StandardOutputText);
    }

    [TestMethod]
    public async Task RunAsync_IncludeToOutputFile_WritesTheHeaderLinesAsTheyCome()
    {
        await RunAsync(["-s", "-i", "-o", "out.txt", "http://127.0.0.1:8099/d/a"], "http");

        Assert.AreEqual(Head, Encoding.Latin1.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
    }

    private Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        string scheme,
        bool runsOnWindows = false,
        bool standardOutputIsTerminal = true,
        bool terminalRendersStyles = true,
        Func<string, string?>? readEnvironmentVariable = null)
    {
        RecordingProtocolHandler handler = new(scheme, async context =>
        {
            await context.HeaderOutput!.WriteAsync(Encoding.Latin1.GetBytes(Head), context.CancellationToken);
            return TransferResult.Success(0);
        });

        return new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                outputFiles,
                outputFiles,
                standardOutput,
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows,
                standardOutputIsTerminal: standardOutputIsTerminal,
                readEnvironmentVariable: readEnvironmentVariable,
                terminalRendersStyles: terminalRendersStyles)
            .RunAsync(arguments);
    }
}
