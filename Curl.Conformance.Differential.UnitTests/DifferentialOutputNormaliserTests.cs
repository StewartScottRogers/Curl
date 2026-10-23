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
