using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins CGR-285 (BL-3814): under <c>--trace-config doh</c> alone curl writes the DoH sub-transfer's lines and no
/// <c>[DNS]</c> filter lines, and under <c>dns</c> alone the <c>[DNS]</c> filter lines and no sub-transfer lines.
/// </summary>
[TestClass]
public sealed class Cgr285Tests
{
    [TestMethod]
    public void DohOnly_WritesSubTransferLines_AndNoDnsFilterLines()
    {
        string written = Write(writesDnsFilterLines: false, writesSubTransferLines: true);

        Assert.IsFalse(written.Contains("[DNS]", StringComparison.Ordinal), written);
        Assert.IsTrue(written.Contains("* [DoH] connected", StringComparison.Ordinal), written);
        Assert.IsTrue(written.Contains("> GET /dns-query", StringComparison.Ordinal), written);
    }

    [TestMethod]
    public void DnsOnly_WritesDnsFilterLines_AndNoSubTransferLines()
    {
        string written = Write(writesDnsFilterLines: true, writesSubTransferLines: false);

        Assert.IsTrue(written.Contains("* [DNS] created DNS filter for foo.test:80", StringComparison.Ordinal), written);
        Assert.IsFalse(written.Contains("[DoH] connected", StringComparison.Ordinal), written);
        Assert.IsFalse(written.Contains("GET /dns-query", StringComparison.Ordinal), written);
        Assert.IsFalse(written.Contains("HTTP/1.1 200", StringComparison.Ordinal), written);
    }

    [TestMethod]
    public void BothComponents_WriteEveryLine()
    {
        string written = Write(writesDnsFilterLines: true, writesSubTransferLines: true);

        Assert.IsTrue(written.Contains("[DNS] created DNS filter", StringComparison.Ordinal), written);
        Assert.IsTrue(written.Contains("[DoH] connected", StringComparison.Ordinal), written);
        Assert.IsTrue(written.Contains("HTTP/1.1 200", StringComparison.Ordinal), written);
    }

    private static string Write(bool writesDnsFilterLines, bool writesSubTransferLines)
    {
        MemoryStream output = new();
        DohSubTransferEvents events = new(new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel), TlsBackend.Schannel, writesDnsFilterLines, writesSubTransferLines);
        events.ReportInfo("[DNS] created DNS filter for foo.test:80, transport=3, queries=3");
        events.ReportInfo("[DNS] added");
        events.ReportInfo("connected");
        events.ReportTlsData("tls"u8, sent: true);
        events.ReportRequestHeader("GET /dns-query HTTP/1.1\r\n"u8);
        events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
        events.ReportDataSent("d"u8);
        events.ReportDataReceived("r"u8);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
