using System.Text;

namespace Curl.Conformance.Differential.UnitTests;

[TestClass]
public sealed class DifferentialOutputNormaliserTests
{
    private const string WorkingDirectory = @"C:\t\A\case";

    [TestMethod]
    public void Normalise_RunToRunValues_AreReplaced()
    {
        var output = Normalise(
            "--------------------------abcdefghijABCDEFGHIJ01 cnonce=\"1a\" response=\"ff\" C:\\t\\A\\case\\x C:/t/A/case/y "
            + "12:34:56.123456 from 127.0.0.1 port 51234 after 17 ms\n\xff");

        Assert.AreEqual("<BOUNDARY> cnonce=\"X\" response=\"X\" <CWD>\\x <CWD>/y <TS> from 127.0.0.1 port N after N ms\n\xff", output);
    }

    [TestMethod]
    public void Normalise_DebugBuildLines_AreRemoved()
    {
        var output = Normalise(
            "WARNING: this libcurl is Debug-enabled, do not use in production\r\n\r\n"
            + "* !!! WARNING !!!\r\n* This is a debug build of libcurl, do not use in production.\r\n"
            + "* Curl_xfer_send(len=5)\n* sending last upload chunk of 5 bytes\n"
            + "{ [41 bytes data]\r\n* sendrecv_dl() -> 23\r\n* Curl_sendrecv() -> 23\r\n* closing connection #0\r\n");

        Assert.AreEqual("{ [41 bytes data]\r\n* closing connection #0\r\n", output);
    }

    [TestMethod]
    public void Normalise_ProgressMeter_BecomesOnePlaceholder()
    {
        var output = Normalise("  % Total    % Received % Xferd  Average Speed\r\n\r  0     0\r100    5\nok");

        Assert.AreEqual("<PROGRESS>\nok", output);
    }

    [TestMethod]
    public void Normalise_VerboseProgressLinesAfterTraceLines_DifferingOnlyInSpeed_NormaliseEqual()
    {
        const string Prefix = "{ [3 bytes data]\r\n";
        var ours = Normalise(Prefix + "\r100      3 100      3   0      0     11      0 --:--:-- --:--:-- --:--:--    11\r\n");
        var theirs = Normalise(Prefix + "\r100      3 100      3   0      0    173      0 --:--:-- --:--:-- --:--:--   173\r\n");

        Assert.AreEqual(theirs, ours);
        Assert.AreEqual(Prefix + "<PROGRESS>\r\n", ours);
    }

    [TestMethod]
    public void Normalise_VerboseProgressMeterLinesAfterOtherOutput_IgnoresSpeedColumns()
    {
        const string Before = "{ [3 bytes data]\r\n\r100      3 100      3   0      0     11      0 --:--:-- --:--:-- --:--:--    11\r\n* closed\r\n";
        const string After = "{ [3 bytes data]\r\n\r100      3 100      3   0      0    173      0 --:--:-- --:--:-- --:--:--   173\r\n* closed\r\n";

        Assert.AreEqual(Normalise(Before), Normalise(After));
        Assert.AreEqual("{ [3 bytes data]\r\n<PROGRESS>\r\n* closed\r\n", Normalise(Before));
    }

    [TestMethod]
    public void Normalise_ElapsedMillisecondsInATimeoutMessage_AreReplaced()
    {
        Assert.AreEqual("curl: (28) Operation timed out after N milliseconds with 0 bytes received", Normalise("curl: (28) Operation timed out after 1002 milliseconds with 0 bytes received"));
    }

    [TestMethod]
    public void Normalise_TimesInWriteOutAndJson_AreReplaced()
    {
        Assert.AreEqual("<SECONDS><SECONDS> speed_download=N|{\"curl_version\":\"V\",\"speed_download\":N,\"local_port\":N,\"time_total\":<SECONDS>,\"size_download\":5}",
            Normalise("0.0001230.004567 speed_download=793|{\"curl_version\":\"libcurl/8 zlib\",\"speed_download\":1234,\"local_port\":50123,\"time_total\":0.012345,\"size_download\":5}"));
    }

    [TestMethod]
    public void Normalise_AnOutputOfOnlyAPortOrSpeed_IsReplacedButAStatusCodeIsNot()
    {
        Assert.AreEqual("<NUMBER>", Normalise("51234"));
        Assert.AreEqual("200", Normalise("200"));
        Assert.AreEqual("a 51234", Normalise("a 51234"));
    }

    [TestMethod]
    public void Normalise_ProxyProtocolSourcePort_IsReplaced()
    {
        Assert.AreEqual("PROXY TCP4 1.2.3.4 127.0.0.1 N 18080\r\n", Normalise("PROXY TCP4 1.2.3.4 127.0.0.1 53311 18080\r\n"));
    }

    [TestMethod]
    public void Normalise_WebSocketKeyAndAccept_AreReplaced()
    {
        Assert.AreEqual("Sec-WebSocket-Key: <KEY>\r\nsec-websocket-accept: <KEY>\r\nHost: x", Normalise("Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nsec-websocket-accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=\r\nHost: x"));
    }

    [TestMethod]
    public void Normalise_CookieJarExpiry_IsReplacedButASessionCookieIsNot()
    {
        Assert.AreEqual("h\tFALSE\t/\tFALSE\t<EXPIRY>\ta\t1\nh\tFALSE\t/\tFALSE\t0\tb\t2", Normalise("h\tFALSE\t/\tFALSE\t1792790001\ta\t1\nh\tFALSE\t/\tFALSE\t0\tb\t2"));
    }

    [TestMethod]
    public void Normalise_IfModifiedSince_IsReplaced()
    {
        Assert.AreEqual("If-Modified-Since: <DATE>\r\nX: y", Normalise("If-Modified-Since: Fri, 23 Oct 2026 10:00:01 GMT\r\nX: y"));
    }

    [TestMethod]
    public void Normalise_DebugBuildTraceLinesWithPrefixes_AreRemovedWithTheirPrefixes()
    {
        Assert.AreEqual("<TS> [0-0] == Info: Host x\n", Normalise("12:00:00.000001 [0-0] == Info: !!! WARNING !!!\n12:00:00.000002 [0-0] == Info: This is a debug build of libcurl, do not use in production.\n12:00:00.000003 [0-0] == Info: Host x\n"));
        Assert.AreEqual("[0-1] => Send", Normalise("[0-1] [0-1] * Curl_xfer_send(len=5)\n[0-1] * sendrecv_dl() -> 5\n[0-1] => Send"));
    }

    [TestMethod]
    public void Normalise_Run_NormalisesEveryOutput()
    {
        var bytes = Encoding.Latin1.GetBytes("after 3 ms");
        var run = new DifferentialRunResult(0, bytes, bytes, new Dictionary<string, byte[]> { ["f"] = bytes }, bytes, TimeSpan.Zero);

        var normalised = DifferentialOutputNormaliser.Normalise(run, WorkingDirectory);

        foreach (var output in new[] { normalised.StandardOutput, normalised.StandardError, normalised.Files["f"], normalised.Wire })
        {
            Assert.AreEqual("after N ms", Encoding.Latin1.GetString(output));
        }
    }

    private static string Normalise(string text) =>
        Encoding.Latin1.GetString(DifferentialOutputNormaliser.Normalise(Encoding.Latin1.GetBytes(text), WorkingDirectory));
}
