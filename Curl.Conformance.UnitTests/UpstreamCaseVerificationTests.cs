using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamCaseVerification"/> against how <c>runtests.pl</c> checks a finished
/// case: the protocol, the reply data, the standard streams, the files and the exit code.
/// </summary>
[TestClass]
public sealed class UpstreamCaseVerificationTests
{
    [TestMethod]
    public void FindFirstDifference_NothingToVerifyAndExitZero_ReturnsNull()
    {
        Assert.IsNull(Verify("", Run()));
    }

    [TestMethod]
    public void FindFirstDifference_Protocol_StripsLinesFromBothSidesAndRunsStrippartOnTheReceivedBytes()
    {
        string sections = "<verify>\n<protocol crlf=\"yes\">\nGET / HTTP/1.1\nDate: expected\nUser-Agent: curl/x\n</protocol>\n"
            + "<strip>\n^Date:\n</strip>\n<strippart>\ns/curl\\/[0-9.]+/curl\\/x/\n# comment\n</strippart>\n</verify>\n";

        Assert.IsNull(Verify(sections, Run(received: "GET / HTTP/1.1\r\nDate: now\r\nUser-Agent: curl/8.21.0\r\n")));
    }

    [TestMethod]
    public void FindFirstDifference_ProtocolWithCrlfAndNonewline_CutsTheLastLineFeedBeforeForcingCrlf()
    {
        string sections = "<verify>\n<protocol crlf=\"yes\" nonewline=\"yes\">\nPOST / HTTP/1.1\n\nbody\n</protocol>\n</verify>\n";

        Assert.IsNull(Verify(sections, Run(received: "POST / HTTP/1.1\r\n\r\nbody")));
    }

    [TestMethod]
    public void FindFirstDifference_ProtocolThatDiffers_NamesIt()
    {
        string sections = "<verify>\n<protocol nonewline=\"yes\">\nGET / HTTP/1.1\n</protocol>\n</verify>\n";

        Assert.AreEqual(
            "<verify><protocol> differs at byte 5 (line 1): expected \"GET / HTTP/1.1\", got \"GET /x HTTP/1.1\"",
            Verify(sections, Run(received: "GET /x HTTP/1.1")));
    }

    [TestMethod]
    public void FindFirstDifference_ReplyData_IsComparedWithTheOutputFile()
    {
        string sections = "<reply>\n<data crlf=\"headers\">\nHTTP/1.1 200 OK\n\nbody\n</data>\n</reply>\n";

        Assert.IsNull(Verify(sections, Run(outputFile: "HTTP/1.1 200 OK\r\n\r\nbody\n")));
        Assert.AreEqual(
            "the --output file against <reply><data> differs at byte 0 (line 1): expected \"HTTP/1.1 200 OK\\r\\n\", got the end",
            Verify(sections, Run()));
    }

    [TestMethod]
    public void FindFirstDifference_Base64ReplyData_IsComparedDecoded()
    {
        string sections = "<reply>\n<data base64=\"yes\">\naGk=\n</data>\n</reply>\n";

        Assert.IsNull(Verify(sections, Run(outputFile: "hi")));
    }

    [TestMethod]
    public void FindFirstDifference_StripPatternThatRunsTooLong_NamesIt()
    {
        string sections = "<verify>\n<protocol>\nx\n</protocol>\n<strip>\n^(a+)+$\n</strip>\n</verify>\n";

        string? difference = Verify(sections, Run(received: new string('a', 40) + "!\n"));

        Assert.AreEqual("the strip pattern ^(a+)+$ took longer than 1 seconds", difference);
    }

    [TestMethod]
    public void FindFirstDifference_ReplyDataMarkedNocheck_IsNotCompared()
    {
        Assert.IsNull(Verify("<reply>\n<data nocheck=\"yes\">\nx\n</data>\n</reply>\n", Run()));
    }

    [TestMethod]
    public void FindFirstDifference_EmptyReplyData_IsComparedOnlyUnderSendzero()
    {
        Assert.IsNull(Verify("<reply>\n<data>\n</data>\n</reply>\n", Run(outputFile: "x")));
        Assert.IsNotNull(Verify("<reply>\n<data sendzero=\"yes\">\n</data>\n</reply>\n", Run(outputFile: "x")));
    }

    [TestMethod]
    public void FindFirstDifference_Datacheck_ReplacesDataAndIsFollowedByItsNumberedParts()
    {
        string sections = "<reply>\n<data>\nserved\n</data>\n<datacheck nonewline=\"yes\">\none\n</datacheck>\n<datacheck2>\ntwo\n</datacheck2>\n</reply>\n";

        Assert.IsNull(Verify(sections, Run(outputFile: "onetwo\n")));
    }

    [TestMethod]
    public void FindFirstDifference_ReplyDataInTextMode_IgnoresLineEndings()
    {
        Assert.IsNull(Verify("<reply>\n<data mode=\"text\">\na\nb\n</data>\n</reply>\n", Run(outputFile: "a\r\nb\n")));
    }

    [TestMethod]
    public void FindFirstDifference_Stdout_CutsTheNewlineBeforeForcingCrlfAndRunsStripfile()
    {
        string sections = "<verify>\n<stdout crlf=\"yes\" nonewline=\"yes\">\nline\nlast\n</stdout>\n<stripfile>\ns/^noise\\n//\n</stripfile>\n</verify>\n";

        Assert.IsNull(Verify(sections, Run(standardOutput: "noise\nline\r\nlast")));
    }

    [TestMethod]
    public void FindFirstDifference_StdoutThatDiffers_NamesIt()
    {
        Assert.AreEqual(
            "<verify><stdout> differs at byte 0 (line 1): expected \"a\\n\", got \"b\\n\"",
            Verify("<verify>\n<stdout>\na\n</stdout>\n</verify>\n", Run(standardOutput: "b\n")));
    }

    [TestMethod]
    public void FindFirstDifference_StderrInTextMode_IgnoresLineEndings()
    {
        string sections = "<verify>\n<stderr mode=\"text\">\ncurl: (6) x\n</stderr>\n</verify>\n";

        Assert.IsNull(Verify(sections, Run(standardError: "curl: (6) x\r\n")));
        Assert.IsNotNull(Verify(sections, Run(standardError: "curl: (7) x\r\n")));
    }

    [TestMethod]
    public void FindFirstDifference_File_IsComparedWithItsStripfile()
    {
        string path = TemporaryFile("keep\ndrop\n");
        string sections = $"<verify>\n<file1 name=\"{path}\">\nkeep\n</file1>\n<stripfile1>\ns/^drop\\n//\n</stripfile1>\n</verify>\n";

        Assert.IsNull(Verify(sections, Run()));
    }

    [TestMethod]
    public void FindFirstDifference_MissingFile_ComparesAsEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}").Replace('\\', '/');

        Assert.AreEqual(
            $"<verify><file> ({path}) differs at byte 0 (line 1): expected \"x\\n\", got the end",
            Verify($"<verify>\n<file name=\"{path}\">\nx\n</file>\n</verify>\n", Run()));
    }

    [TestMethod]
    public void FindFirstDifference_Notexists_NamesAFileThatExists()
    {
        string path = TemporaryFile("x");
        string missing = path + ".missing";

        Assert.IsNull(Verify($"<verify>\n<notexists>\n{missing}\n</notexists>\n</verify>\n", Run()));
        Assert.AreEqual($"<verify><notexists>: {path} exists", Verify($"<verify>\n<notexists>\n{missing}\n{path}\n</notexists>\n</verify>\n", Run()));
    }

    [TestMethod]
    public void FindFirstDifference_ExitCode_IsComparedWithErrorcodeOrZero()
    {
        Assert.IsNull(Verify("<verify>\n<errorcode>\n6\n</errorcode>\n</verify>\n", Run(exitCode: 6)));
        Assert.AreEqual("<verify><errorcode>: expected exit code 0, got 6", Verify("", Run(exitCode: 6)));
    }

    private static string? Verify(string sections, UpstreamCaseRun run) =>
        UpstreamCaseVerification.FindFirstDifference(ParsedTestCase.From(sections), run);

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
