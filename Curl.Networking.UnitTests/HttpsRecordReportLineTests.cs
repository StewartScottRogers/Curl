using System.Net;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the three forms of curl 8.22.0's <c>-v</c> <c>HTTPS-RR &lt;host&gt;:&lt;port&gt;:</c> line, as
/// <c>cf_dns_report</c> in <c>lib/vdns/cf-dns.c</c> writes it (ADR-0727's BL-2980 amendment).
/// </summary>
[TestClass]
public sealed class HttpsRecordReportLineTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Format_WithNoRecord_WritesADash()
    {
        Diagnostics.Arrange("record", "(none)");

        var line = HttpsRecordReportLine.Format("foo.example.com", 443, null);

        Diagnostics.Assert("line", "HTTPS-RR foo.example.com:443: -", line);
        Assert.AreEqual("HTTPS-RR foo.example.com:443: -", line);
    }

    [TestMethod]
    public void Format_WithARecordForTheOriginItself_WritesTheRecordAsCurlPrintsIt()
    {
        var record = new ServiceBindingRecord(1, ".") { ApplicationProtocols = ["h3", "h2"], Port = 443, IPv4Hints = [IPAddress.Loopback] };
        Diagnostics.Arrange("record", "1 . alpn=h3,h2 port=443 ipv4hint=127.0.0.1");

        var line = HttpsRecordReportLine.Format("foo.example.com", 443, record);

        Diagnostics.Assert("line", "HTTPS-RR foo.example.com:443: 1 . alpn=h3,h2 port=443 ipv4hint=127.0.0.1", line);
        Assert.AreEqual("HTTPS-RR foo.example.com:443: 1 . alpn=h3,h2 port=443 ipv4hint=127.0.0.1", line);
    }

    [TestMethod]
    public void Format_WithARecordThatHadAMandatoryList_WritesMandatoryKeysIgnored()
    {
        var record = new ServiceBindingRecord(1, ".") { HasMandatoryKeys = true, ApplicationProtocols = ["h2"] };
        Diagnostics.Arrange("record", "1 . mandatory=alpn alpn=h2");

        var line = HttpsRecordReportLine.Format("foo.example.com", 443, record);

        Diagnostics.Assert("line", "HTTPS-RR foo.example.com:443: 1 . mandatory-keys(ignored) alpn=h2", line);
        Assert.AreEqual("HTTPS-RR foo.example.com:443: 1 . mandatory-keys(ignored) alpn=h2", line);
    }

    [TestMethod]
    [DataRow("", null, DisplayName = "an empty target and no port")]
    [DataRow(".", 41443, DisplayName = "the root target and the origin's port")]
    public void Format_WithAnApplicableRecord_WritesTheRecord(string target, int? port)
    {
        var record = new ServiceBindingRecord(2, target) { Port = port is { } given ? (ushort)given : null };
        Diagnostics.Arrange("record", $"target '{target}', port {port?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}");

        var line = HttpsRecordReportLine.Format("foo.example.com", 41443, record);

        Diagnostics.Act("line", line);
        Assert.StartsWith("HTTPS-RR foo.example.com:41443: 2 ", line);
        Assert.DoesNotContain("not applicable", line);
    }

    [TestMethod]
    [DataRow("alt.example.com", null, DisplayName = "another target")]
    [DataRow(".", 8443, DisplayName = "another port")]
    public void Format_WithARecordNamingAnotherTargetOrPort_WritesNotApplicable(string target, int? port)
    {
        var record = new ServiceBindingRecord(1, target) { Port = port is { } given ? (ushort)given : null };
        Diagnostics.Arrange("record", $"target '{target}', port {port?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}");

        var line = HttpsRecordReportLine.Format("foo.example.com", 443, record);

        Diagnostics.Assert("line", "HTTPS-RR foo.example.com:443: not applicable", line);
        Assert.AreEqual("HTTPS-RR foo.example.com:443: not applicable", line);
    }
}
