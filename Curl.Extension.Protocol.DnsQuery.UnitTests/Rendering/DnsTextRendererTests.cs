using Curl.Extension.Protocol.DnsQuery.Rendering;
using Curl.Extension.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Extension.Protocol.DnsQuery.Wire;

namespace Curl.Extension.Protocol.DnsQuery.UnitTests.Rendering;

[TestClass]
public sealed class DnsTextRendererTests
{
    [TestMethod]
    public void Render_ResponseWithOneAnswer_WritesDigLikeHeaderQuestionAndAnswer()
    {
        string text = DnsTextRenderer.Render(DnsSampleRecords.Decode("A"));

        Assert.AreEqual(
            ";; ->>HEADER<<- opcode: QUERY, status: NOERROR, id: 4660\n"
            + ";; flags: qr rd ra; QUERY: 1, ANSWER: 1, AUTHORITY: 0, ADDITIONAL: 0\n"
            + "\n;; QUESTION SECTION:\n;example.com.\t\tIN\tA\n"
            + "\n;; ANSWER SECTION:\nexample.com.\t300\tIN\tA\t93.184.216.34\n",
            text);
    }

    [TestMethod]
    public void Render_HeaderOnlyMessage_WritesNoSections()
    {
        DnsMessage message = DnsMessageDecoder.Decode(new DnsWire().Header(9, 0x0005, 0, 0, 0, 0).ToArray()).Value!;

        Assert.AreEqual(
            ";; ->>HEADER<<- opcode: QUERY, status: REFUSED, id: 9\n;; flags:; QUERY: 0, ANSWER: 0, AUTHORITY: 0, ADDITIONAL: 0\n",
            DnsTextRenderer.Render(message));
    }

    [TestMethod]
    public void Render_AuthorityAndAdditionalRecords_WritesEachSection()
    {
        byte[] message = new DnsWire()
            .Header(1, 0x8403, 0, 0, 1, 1)
            .Record(DnsWire.Name("example.com"), 6, 1, 900, DnsWire.Join(DnsWire.Name("ns1"), DnsWire.Name("h"), new byte[20]))
            .Record(DnsWire.Name(""), 41, 1232, 0, [])
            .ToArray();

        string text = DnsTextRenderer.Render(DnsMessageDecoder.Decode(message).Value!);

        StringAssert.EndsWith(
            text,
            ";; flags: qr aa; QUERY: 0, ANSWER: 0, AUTHORITY: 1, ADDITIONAL: 1\n"
            + "\n;; AUTHORITY SECTION:\nexample.com.\t900\tIN\tSOA\tns1. h. 0 0 0 0 0\n"
            + "\n;; ADDITIONAL SECTION:\n.\t0\tCLASS1232\tOPT\t\\# 0\n");
        StringAssert.StartsWith(text, ";; ->>HEADER<<- opcode: QUERY, status: NXDOMAIN, id: 1\n");
    }

    [TestMethod]
    [DataRow("A", "A\t93.184.216.34")]
    [DataRow("AAAA", "AAAA\t2001:db8::1")]
    [DataRow("NS", "NS\tns1.example.com.")]
    [DataRow("CNAME", "CNAME\twww.example.net.")]
    [DataRow("PTR", "PTR\ta\\.b\\032.example.")]
    [DataRow("MX", "MX\t10 mail.example.com.")]
    [DataRow("TXT", "TXT\t\"v=spf1 -all\" \"a\\\"b\\\\\" \"\\255\"")]
    [DataRow("SOA", "SOA\tns1.example.com. hostmaster.example.com. 2024010101 7200 3600 1209600 300")]
    [DataRow("SRV", "SRV\t10 20 443 sip.example.com.")]
    [DataRow("CAA", "CAA\t128 issue \"letsencrypt.org\"")]
    [DataRow("TYPE65", "TYPE65\t\\# 2 01AB")]
    [DataRow("TYPE99", "TYPE99\t\\# 0")]
    public void Render_EachRecordType_WritesItsRdataInPresentationFormat(string sample, string expected)
    {
        string answer = DnsTextRenderer.Render(DnsSampleRecords.Decode(sample)).Split('\n')[^2];

        Assert.AreEqual("example.com.\t300\tIN\t" + expected, answer);
    }
}
