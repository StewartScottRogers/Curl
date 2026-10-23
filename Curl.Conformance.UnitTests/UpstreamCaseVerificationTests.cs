using System.Text;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCaseVerification"/> against how <c>runtests.pl</c> checks a finished
/// case: the protocol, the reply data, the standard streams, the files and the exit code.
/// </summary>
[TestClass]
public sealed class UpstreamCaseVerificationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void FindFirstDifference_NothingToVerifyAndExitZero_ReturnsNull()
    {
        string? difference = Verify("", Run());
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_Protocol_StripsLinesFromBothSidesAndRunsStrippartOnTheReceivedBytes()
    {
        string sections = "<verify>\n<protocol crlf=\"yes\">\nGET / HTTP/1.1\nDate: expected\nUser-Agent: curl/x\n</protocol>\n"
            + "<strip>\n^Date:\n</strip>\n<strippart>\ns/curl\\/[0-9.]+/curl\\/x/\n# comment\n</strippart>\n</verify>\n";

        string? difference = Verify(sections, Run(received: "GET / HTTP/1.1\r\nDate: now\r\nUser-Agent: curl/8.22.0\r\n"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_ProtocolWithCrlfAndNonewline_CutsTheLastLineFeedBeforeForcingCrlf()
    {
        string sections = "<verify>\n<protocol crlf=\"yes\" nonewline=\"yes\">\nPOST / HTTP/1.1\n\nbody\n</protocol>\n</verify>\n";

        string? difference = Verify(sections, Run(received: "POST / HTTP/1.1\r\n\r\nbody"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_ProtocolThatDiffers_NamesIt()
    {
        string sections = "<verify>\n<protocol nonewline=\"yes\">\nGET / HTTP/1.1\n</protocol>\n</verify>\n";

        string? difference = Verify(sections, Run(received: "GET /x HTTP/1.1"));
        ExpectDifference("<verify><protocol> differs at byte 5 (line 1): expected \"GET / HTTP/1.1\", got \"GET /x HTTP/1.1\"", FirstLine(difference));
        Assert.AreEqual("<verify><protocol> differs at byte 5 (line 1): expected \"GET / HTTP/1.1\", got \"GET /x HTTP/1.1\"", FirstLine(difference));
    }

    [TestMethod]
    public void FindFirstDifference_ProxyThatDiffers_ComparesItAgainstTheProxyBytes()
    {
        string sections = "<verify>\n<proxy nonewline=\"yes\">\nCONNECT a:1 HTTP/1.1\n</proxy>\n</verify>\n";

        string? matching = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { ProxyReceivedBytes = Bytes("CONNECT a:1 HTTP/1.1") });
        string? differing = Verify(sections, Run(received: "CONNECT a:1 HTTP/1.1"));

        Assert.IsNull(matching);
        Assert.AreEqual("<verify><proxy> differs at byte 0 (line 1): expected \"CONNECT a:1 HTTP/1.1\", got the end", FirstLine(differing));
    }

    [TestMethod]
    public void FindFirstDifference_Upload_ComparesItAgainstTheUploadedMessageWithoutStrip()
    {
        string sections = "<verify>\n<strip>\n^body\n</strip>\n<upload crlf=\"yes\">\nbody\n.\n</upload>\n</verify>\n";

        string? matching = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { UploadedBytes = Bytes("body\r\n.\r\n") });
        string? differing = Verify(sections, Run(received: "body\r\n.\r\n"));

        Assert.IsNull(matching);
        Assert.AreEqual("<verify><upload> differs at byte 0 (line 1): expected \"body\\r\\n\", got the end", FirstLine(differing));
    }

    [TestMethod]
    public void FindFirstDifference_Upload_RunsStrippartOnTheUploadedMessageBeforeComparing()
    {
        string sections = "<verify>\n<strippart>\ns/boundary=-*[A-Za-z0-9]*/boundary=----/\n</strippart>\n"
            + "<upload crlf=\"yes\">\nContent-Type: multipart/mixed; boundary=----\n.\n</upload>\n</verify>\n";

        string? matching = Verify(sections, new UpstreamCaseRun(0, [], [], [], [])
        {
            UploadedBytes = Bytes("Content-Type: multipart/mixed; boundary=------------aB3dE6gH9\r\n.\r\n"),
        });

        Assert.IsNull(matching);
    }

    [TestMethod]
    public void FindFirstDifference_Upload_RunsStrippartOnTheUploadedMessage()
    {
        // test646: runtests.pl runs <strippart> on the stored upload, which cuts curl's random MIME boundary.
        string sections = "<verify>\n<strippart>\ns/boundary=-+[A-Za-z0-9]*/boundary=---/\n</strippart>\n"
            + "<upload crlf=\"yes\">\nContent-Type: multipart/mixed; boundary=---\n</upload>\n</verify>\n";

        string? difference = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { UploadedBytes = Bytes("Content-Type: multipart/mixed; boundary=------Ab12\r\n") });
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_ReplyData_IsComparedWithTheOutputFile()
    {
        string sections = "<reply>\n<data crlf=\"headers\">\nHTTP/1.1 200 OK\n\nbody\n</data>\n</reply>\n";

        string? difference = Verify(sections, Run(outputFile: "HTTP/1.1 200 OK\r\n\r\nbody\n"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
        string? difference2 = Verify(sections, Run());
        ExpectDifference("the --output file against <reply><data> differs at byte 0 (line 1): expected \"HTTP/1.1 200 OK\\r\\n\", got the end", FirstLine(difference2));
        Assert.AreEqual("the --output file against <reply><data> differs at byte 0 (line 1): expected \"HTTP/1.1 200 OK\\r\\n\", got the end", FirstLine(difference2));
    }

    [TestMethod]
    public void FindFirstDifference_Base64ReplyData_IsComparedDecoded()
    {
        string sections = "<reply>\n<data base64=\"yes\">\naGk=\n</data>\n</reply>\n";

        string? difference = Verify(sections, Run(outputFile: "hi"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_StripPatternThatRunsTooLong_NamesIt()
    {
        string sections = "<verify>\n<protocol>\nx\n</protocol>\n<strip>\n^(a+)+$\n</strip>\n</verify>\n";

        string? difference = Verify(sections, Run(received: new string('a', 40) + "!\n"), TimeSpan.FromSeconds(1));

        ExpectDifference("the strip pattern ^(a+)+$ took longer than 1 seconds", difference);
        Assert.AreEqual("the strip pattern ^(a+)+$ took longer than 1 seconds", difference);
    }

    [TestMethod]
    public void FindFirstDifference_ReplyDataMarkedNocheck_IsNotCompared()
    {
        string? difference = Verify("<reply>\n<data nocheck=\"yes\">\nx\n</data>\n</reply>\n", Run());
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_EmptyReplyData_IsComparedOnlyUnderSendzero()
    {
        string? difference = Verify("<reply>\n<data>\n</data>\n</reply>\n", Run(outputFile: "x"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
        string? difference2 = Verify("<reply>\n<data sendzero=\"yes\">\n</data>\n</reply>\n", Run(outputFile: "x"));
        ExpectPresent(difference2);
        Assert.IsNotNull(difference2);
    }

    [TestMethod]
    public void FindFirstDifference_Datacheck_ReplacesDataAndIsFollowedByItsNumberedParts()
    {
        string sections = "<reply>\n<data>\nserved\n</data>\n<datacheck nonewline=\"yes\">\none\n</datacheck>\n<datacheck2>\ntwo\n</datacheck2>\n</reply>\n";

        string? difference = Verify(sections, Run(outputFile: "onetwo\n"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_ReplyDataInTextMode_IgnoresLineEndings()
    {
        string? difference = Verify("<reply>\n<data mode=\"text\">\na\nb\n</data>\n</reply>\n", Run(outputFile: "a\r\nb\n"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_Stdout_CutsTheNewlineBeforeForcingCrlfAndRunsStripfile()
    {
        string sections = "<verify>\n<stdout crlf=\"yes\" nonewline=\"yes\">\nline\nlast\n</stdout>\n<stripfile>\ns/^noise\\n//\n</stripfile>\n</verify>\n";

        string? difference = Verify(sections, Run(standardOutput: "noise\nline\r\nlast"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_StdoutOfEmptyMarker_ExpectsNoOutput()
    {
        string sections = "<verify>\n<stdout>\n%EMPTY\n</stdout>\n</verify>\n";

        string? matching = Verify(sections, Run(standardOutput: ""));
        string? differing = Verify(sections, Run(standardOutput: "%EMPTY\n"));

        ExpectDifference(null, matching);
        Assert.IsNull(matching);
        Assert.IsNotNull(differing);
    }

    // runtests.pl reads stdout only from <verify>: a <client><stdout> is never compared (BL-2221).
    [TestMethod]
    public void FindFirstDifference_ClientStdout_IsNotComparedWhetherOrNotTheOutputMatches()
    {
        string sections = "<client>\n<stdout>\ndata blobb\n</stdout>\n</client>\n";

        string? matching = Verify(sections, Run(standardOutput: "data blobb\n"));
        string? differing = Verify(sections, Run(standardOutput: "something else\n"));

        ExpectDifference(null, differing);
        Assert.IsNull(matching);
        Assert.IsNull(differing);
    }

    [TestMethod]
    public void FindFirstDifference_StdoutThatDiffers_NamesIt()
    {
        string? difference = Verify("<verify>\n<stdout>\na\n</stdout>\n</verify>\n", Run(standardOutput: "b\n"));
        ExpectDifference("<verify><stdout> differs at byte 0 (line 1): expected \"a\\n\", got \"b\\n\"", FirstLine(difference));
        Assert.AreEqual("<verify><stdout> differs at byte 0 (line 1): expected \"a\\n\", got \"b\\n\"", FirstLine(difference));
    }

    // Curl's --help adds lines naming --ai-help (BL-2985); test1461 compares curl's own lines only.
    [TestMethod]
    public void FindFirstDifference_StdoutWithCurlOnlyAiHelpLines_ComparesTheRest()
    {
        string sections = "<verify>\n<stdout mode=\"text\">\nUse \"--help all\" to list all options\n</stdout>\n</verify>\n";
        string help = "Use \"--help all\" to list all options\r\nUse \"--ai-help\" for Markdown help written for AI agents (Curl only)\r\n";

        string? difference = Verify(sections, Run(standardOutput: help));

        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_StdoutWithAnUnexpectedExtraLine_StillDiffers()
    {
        string sections = "<verify>\n<stdout mode=\"text\">\nUse \"--help all\" to list all options\n</stdout>\n</verify>\n";
        string help = "Use \"--help all\" to list all options\r\nUse \"--ai-help\" for Markdown help written for AI agents (Curl only)\r\nextra\r\n";

        string? difference = Verify(sections, Run(standardOutput: help));

        ExpectPresent(difference);
        Assert.IsNotNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_StderrInTextMode_IgnoresLineEndings()
    {
        string sections = "<verify>\n<stderr mode=\"text\">\ncurl: (6) x\n</stderr>\n</verify>\n";

        string? difference = Verify(sections, Run(standardError: "curl: (6) x\r\n"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
        string? difference2 = Verify(sections, Run(standardError: "curl: (7) x\r\n"));
        ExpectPresent(difference2);
        Assert.IsNotNull(difference2);
    }

    [TestMethod]
    public void FindFirstDifference_StderrInWarnMode_ComparesWrappedWarningsAsOneLine()
    {
        // curl 8.22.0's runtests.pl, mode="warn": each line loses "Warning: " and "\r", and its "\n" becomes a space.
        string sections = "<verify>\n<stderr mode=\"warn\">\nWarning: a b c d\n</stderr>\n</verify>\n";

        string? difference = Verify(sections, Run(standardError: "Warning: a b\r\nWarning: c d\r\n"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
        string? difference2 = Verify(sections, Run(standardError: "Warning: a b\r\nWarning: c e\r\n"));
        ExpectPresent(difference2);
        Assert.IsNotNull(difference2);
    }

    [TestMethod]
    public void FindFirstDifference_StderrNamingTheLogDirectory_MatchesCurlsNoteWrappedAt79Columns()
    {
        // test996 with a 100-character %LOGDIR: voutf cuts after "transfer, ", then at the full width.
        string logDirectory = "/" + new string('d', 99);
        string sections = $"<verify>\n<stderr mode=\"text\">\nNote: skips transfer, \"{logDirectory}/there\" exists locally\n</stderr>\n</verify>\n";
        string wrapped = "Note: skips transfer, \nNote: \"/" + new string('d', 71) + "\nNote: " + new string('d', 28) + "/there\" exists locally\n";

        string? difference = Verify(sections, Run(standardError: wrapped, logDirectory: logDirectory));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_StderrNotNamingTheLogDirectory_KeepsCurlsWrappedNote()
    {
        string sections = "<verify>\n<stderr>\nNote: skips transfer, \"there\" exists locally\n</stderr>\n</verify>\n";

        string? difference = Verify(sections, Run(standardError: "Note: skips transfer, \nNote: \"there\" exists locally\n", logDirectory: "/log"));
        ExpectDifference("<verify><stderr> differs at byte 22 (line 1): expected \"Note: skips transfer, \\\"there\\\" exists locally\\n\", got \"Note: skips transfer, \\n\"", FirstLine(difference));
        Assert.IsNotNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_File_IsComparedWithItsStripfile()
    {
        string path = TemporaryFile("keep\ndrop\n");
        string sections = $"<verify>\n<file1 name=\"{path}\">\nkeep\n</file1>\n<stripfile1>\ns/^drop\\n//\n</stripfile1>\n</verify>\n";

        string? difference = Verify(sections, Run());
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_MissingFile_ComparesAsEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}").Replace('\\', '/');

        string? difference = Verify($"<verify>\n<file name=\"{path}\">\nx\n</file>\n</verify>\n", Run());
        ExpectDifference($"<verify><file> ({path}) differs at byte 0 (line 1): expected \"x\\n\", got the end", FirstLine(difference));
        Assert.AreEqual($"<verify><file> ({path}) differs at byte 0 (line 1): expected \"x\\n\", got the end", FirstLine(difference));
    }

    [TestMethod]
    public void FindFirstDifference_Notexists_NamesAFileThatExists()
    {
        string path = TemporaryFile("x");
        string missing = path + ".missing";

        string? difference = Verify($"<verify>\n<notexists>\n{missing}\n</notexists>\n</verify>\n", Run());
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
        string? difference2 = Verify($"<verify>\n<notexists>\n{missing}\n{path}\n</notexists>\n</verify>\n", Run());
        ExpectDifference($"<verify><notexists>: {path} exists", difference2);
        Assert.AreEqual($"<verify><notexists>: {path} exists", difference2);
    }

    [TestMethod]
    public void FindFirstDifference_ExitCode_IsComparedWithErrorcodeOrZero()
    {
        string? difference = Verify("<verify>\n<errorcode>\n6\n</errorcode>\n</verify>\n", Run(exitCode: 6));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
        string? difference2 = Verify("", Run(exitCode: 6));
        ExpectDifference("<verify><errorcode>: expected exit code 0, got 6", difference2);
        Assert.AreEqual("<verify><errorcode>: expected exit code 0, got 6", difference2);
    }

    // runtests.pl splits <errorcode> on commas and accepts any of the listed codes (test305, test405).
    [TestMethod]
    [DataRow(77, null)]
    [DataRow(60, null)]
    [DataRow(35, "<verify><errorcode>: expected exit code 77, 60, got 35")]
    public void FindFirstDifference_ExitCodeList_PassesForAnyListedCode(int exitCode, string? expected)
    {
        string? difference = Verify("<verify>\n<errorcode>\n77, 60\n</errorcode>\n</verify>\n", Run(exitCode: exitCode));

        ExpectDifference(expected, difference);
        Assert.AreEqual(expected, difference);
    }

    // runtests.pl never reads a <verify><data>, so test301's placeholder is not compared with anything.
    [TestMethod]
    public void FindFirstDifference_VerifyDataPlaceholder_IsIgnored()
    {
        string? difference = Verify("<verify>\n<data>\n[insert full protocol verifiction dump here]\n</data>\n</verify>\n", Run(standardOutput: "body\n", outputFile: ""));

        ExpectDifference(null, difference);
        Assert.IsNull(difference);
    }

    [TestMethod]
    public void FindFirstDifference_Socks_IsComparedWithWhatSocksdLogged()
    {
        const string sections = "<verify>\n<socks>\natyp 1 => 12.34.56.78\n</socks>\n</verify>\n";

        string? matching = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { SocksLog = Bytes("atyp 1 => 12.34.56.78\n") });
        string? differing = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { SocksLog = Bytes("atyp 3 => host\n") });

        ExpectDifference(null, matching);
        Assert.IsNull(matching);
        Assert.StartsWith("<verify><socks>", differing!);
    }

    [TestMethod]
    public void FindFirstDifference_Dns_ComparesSortedLinesAndKeepsOnlyThoseItsHostPatternMatches()
    {
        const string sections = "<verify>\n<dns host=\"examplehost.invalid QTYPE A\">\nQNAME examplehost.invalid QTYPE A\nQNAME examplehost.invalid QTYPE AAAA\n</dns>\n</verify>\n";
        const string unfiltered = "<verify>\n<dns>\nQNAME a QTYPE A\n</dns>\n</verify>\n";

        string? matching = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { DnsLog = "QNAME examplehost.invalid QTYPE HTTPS\nQNAME examplehost.invalid QTYPE AAAA\nQNAME examplehost.invalid QTYPE A\n" });
        string? differing = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { DnsLog = "QNAME examplehost.invalid QTYPE A\n" });
        string? unfilteredExtraLine = Verify(unfiltered, new UpstreamCaseRun(0, [], [], [], []) { DnsLog = "QNAME a QTYPE A\nQNAME a QTYPE HTTPS\n" });

        Assert.IsNull(matching);
        Assert.StartsWith("<verify><dns>", differing!);
        Assert.StartsWith("<verify><dns>", unfilteredExtraLine!);
    }

    private string? Verify(string sections, UpstreamCaseRun run) => Verify(sections, run, UpstreamRegex.MatchTimeout);

    private string? Verify(string sections, UpstreamCaseRun run, TimeSpan stripMatchTimeout)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", Neutral(sections));
        string? difference = UpstreamCaseVerification.FindFirstDifference(ParsedTestCase.From(sections), run, stripMatchTimeout);
        diagnostics.Act("first difference", Neutral(difference));
        return difference;
    }

    private void ExpectDifference(string? expected, string? actual) =>
        TestDiagnostics.For(TestContext).Assert("first difference", Neutral(expected), Neutral(actual));

    private void ExpectPresent(string? actual) =>
        TestDiagnostics.For(TestContext).Assert("a difference is reported", true, actual is not null);

    // The sentence naming the first difference; the line diff after it is pinned in UpstreamLineDifferenceTests.
    private static string? FirstLine(string? difference) => difference?.Split('\n')[0];

    [TestMethod]
    public void FindFirstDifference_StdoutWithLoadfile_ExpectsTheNamedFileInPlaceOfTheBody()
    {
        string path = TemporaryFile("one\ntwo\n").Replace('\\', '/');
        string sections = $"<verify>\n<stdout loadfile=\"{path}\">\n</stdout>\n</verify>\n";

        Assert.IsNull(Verify(sections, Run(standardOutput: "one\ntwo\n")));
        ExpectPresent(Verify(sections, Run(standardOutput: "one\n")));
    }

    private static string Neutral(string? text) =>
        (text ?? "(none)").Replace(Path.GetTempPath().Replace('\\', '/'), "<temp>/");

    private static UpstreamCaseRun Run(int exitCode = 0, string standardOutput = "", string standardError = "", string received = "", string outputFile = "", string? logDirectory = null) =>
        new(exitCode, Bytes(standardOutput), Bytes(standardError), Bytes(received), Bytes(outputFile)) { LogDirectory = logDirectory };

    private static string TemporaryFile(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path.Replace('\\', '/');
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
