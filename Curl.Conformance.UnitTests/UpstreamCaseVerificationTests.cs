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

        string? difference = Verify(sections, Run(received: "GET / HTTP/1.1\r\nDate: now\r\nUser-Agent: curl/8.21.0\r\n"));
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
        ExpectDifference("<verify><protocol> differs at byte 5 (line 1): expected \"GET / HTTP/1.1\", got \"GET /x HTTP/1.1\"", difference);
        Assert.AreEqual("<verify><protocol> differs at byte 5 (line 1): expected \"GET / HTTP/1.1\", got \"GET /x HTTP/1.1\"", difference);
    }

    [TestMethod]
    public void FindFirstDifference_ProxyThatDiffers_ComparesItAgainstTheProxyBytes()
    {
        string sections = "<verify>\n<proxy nonewline=\"yes\">\nCONNECT a:1 HTTP/1.1\n</proxy>\n</verify>\n";

        string? matching = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { ProxyReceivedBytes = Bytes("CONNECT a:1 HTTP/1.1") });
        string? differing = Verify(sections, Run(received: "CONNECT a:1 HTTP/1.1"));

        Assert.IsNull(matching);
        Assert.AreEqual("<verify><proxy> differs at byte 0 (line 1): expected \"CONNECT a:1 HTTP/1.1\", got the end", differing);
    }

    [TestMethod]
    public void FindFirstDifference_Upload_ComparesItAgainstTheUploadedMessageWithoutStrip()
    {
        string sections = "<verify>\n<strip>\n^body\n</strip>\n<upload crlf=\"yes\">\nbody\n.\n</upload>\n</verify>\n";

        string? matching = Verify(sections, new UpstreamCaseRun(0, [], [], [], []) { UploadedBytes = Bytes("body\r\n.\r\n") });
        string? differing = Verify(sections, Run(received: "body\r\n.\r\n"));

        Assert.IsNull(matching);
        Assert.AreEqual("<verify><upload> differs at byte 0 (line 1): expected \"body\\r\\n\", got the end", differing);
    }

    [TestMethod]
    public void FindFirstDifference_ReplyData_IsComparedWithTheOutputFile()
    {
        string sections = "<reply>\n<data crlf=\"headers\">\nHTTP/1.1 200 OK\n\nbody\n</data>\n</reply>\n";

        string? difference = Verify(sections, Run(outputFile: "HTTP/1.1 200 OK\r\n\r\nbody\n"));
        ExpectDifference(null, difference);
        Assert.IsNull(difference);
        string? difference2 = Verify(sections, Run());
        ExpectDifference("the --output file against <reply><data> differs at byte 0 (line 1): expected \"HTTP/1.1 200 OK\\r\\n\", got the end", difference2);
        Assert.AreEqual("the --output file against <reply><data> differs at byte 0 (line 1): expected \"HTTP/1.1 200 OK\\r\\n\", got the end", difference2);
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

    [TestMethod]
    public void FindFirstDifference_StdoutThatDiffers_NamesIt()
    {
        string? difference = Verify("<verify>\n<stdout>\na\n</stdout>\n</verify>\n", Run(standardOutput: "b\n"));
        ExpectDifference("<verify><stdout> differs at byte 0 (line 1): expected \"a\\n\", got \"b\\n\"", difference);
        Assert.AreEqual("<verify><stdout> differs at byte 0 (line 1): expected \"a\\n\", got \"b\\n\"", difference);
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
        ExpectDifference($"<verify><file> ({path}) differs at byte 0 (line 1): expected \"x\\n\", got the end", difference);
        Assert.AreEqual($"<verify><file> ({path}) differs at byte 0 (line 1): expected \"x\\n\", got the end", difference);
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

    private static string Neutral(string? text) =>
        (text ?? "(none)").Replace(Path.GetTempPath().Replace('\\', '/'), "<temp>/");

    private static UpstreamCaseRun Run(int exitCode = 0, string standardOutput = "", string standardError = "", string received = "", string outputFile = "") =>
        new(exitCode, Bytes(standardOutput), Bytes(standardError), Bytes(received), Bytes(outputFile));

    private static string TemporaryFile(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path.Replace('\\', '/');
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
