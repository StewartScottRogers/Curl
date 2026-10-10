namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamPerlCheckLine"/>: a <c>%PERL</c> check line reaches the one-liner
/// interpreter or the test610.pl / test613.pl emulation, and any other line is refused.
/// </summary>
[TestClass]
public sealed class UpstreamPerlCheckLineTests
{
    [TestMethod]
    [DataRow("perl -e \"print 'Test requires a Unix system' if($^O eq 'MSWin32');\"", true)]
    [DataRow("perl /tests/libtest/test610.pl mkdir /log/test610.dir", true)]
    [DataRow("perl /tests/libtest/test613.pl prepare /log/test1445.dir", true)]
    [DataRow("perl /tests/libtest/test1013.pl ../curl-config /log/stdout1014 features", false)]
    [DataRow("resolve --ipv6 ip6-localhost", false)]
    public void Interprets_TellsAnEmulatedPerlLineFromAnyOther(string line, bool expected)
    {
        bool interprets = UpstreamPerlCheckLine.Interprets(line);

        Assert.AreEqual(expected, interprets);
    }

    [TestMethod]
    public void RunLine_OneLiner_RunsTheOneLiner()
    {
        UpstreamPerlOneLinerResult? result = UpstreamPerlCheckLine.RunLine("perl -e \"print 'Test requires a Unix system' if($^O eq 'MSWin32');\"", "MSWin32");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, "Test requires a Unix system"), result);
    }

    [TestMethod]
    [DataRow("perl /tests/libtest/test610.pl rm", "Usage: /tests/libtest/test610.pl mkdir|rmdir|rm|move|gone path1 [path2] [more commands...]\n")]
    [DataRow("perl /tests/libtest/test613.pl prepare", "Usage: /tests/libtest/test613.pl prepare|postprocess directory [logfile]\n")]
    public void RunLine_EmulatedScript_RunsTheScript(string line, string expectedOutput)
    {
        UpstreamPerlOneLinerResult? result = UpstreamPerlCheckLine.RunLine(line, "linux");

        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, expectedOutput), result);
    }

    [TestMethod]
    [DataRow("perl /tests/libtest/test1022.pl ../curl-config /log/stdout1023 vernum")]
    [DataRow("resolve --ipv6 ip6-localhost")]
    public void RunLine_LineTheHarnessDoesNotEmulate_IsNotRun(string line)
    {
        UpstreamPerlOneLinerResult? result = UpstreamPerlCheckLine.RunLine(line, "linux");

        Assert.IsNull(result);
    }
}
