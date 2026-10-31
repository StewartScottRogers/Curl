using Curl.Extension.Protocol.DnsQuery.Rendering;
using Curl.Extension.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Extension.Protocol.DnsQuery.Wire;

namespace Curl.Extension.Protocol.DnsQuery.UnitTests.Rendering;

[TestClass]
public sealed class DnsJsonRendererTests
{
    [TestMethod]
    public void Render_HeaderOnlyMessage_WritesEmptySections()
    {
        DnsMessage message = DnsMessageDecoder.Decode(new DnsWire().Header(9, 0x0025, 0, 0, 0, 0).ToArray()).Value!;

        Assert.AreEqual(
            """{"id":9,"opcode":"QUERY","status":"REFUSED","flags":["ad"],"question":[],"answer":[],"authority":[],"additional":[]}""",
            DnsJsonRenderer.Render(message));
    }

    [TestMethod]
    public void Render_AuthorityAndAdditionalRecords_WritesEachSection()
    {
        byte[] message = new DnsWire()
            .Header(1, 0x8403, 0, 0, 1, 1)
            .Record(DnsWire.Name("example.com"), 2, 1, 900, DnsWire.Name("ns1"))
            .Record(DnsWire.Name(""), 41, 1232, 0, [])
            .ToArray();

        Assert.AreEqual(
            """{"id":1,"opcode":"QUERY","status":"NXDOMAIN","flags":["qr","aa"],"question":[],"answer":[],"authority":"""
            + """[{"name":"example.com.","type":"NS","class":"IN","ttl":900,"data":{"nsdname":"ns1."}}],"additional":"""
            + """[{"name":".","type":"OPT","class":"CLASS1232","ttl":0,"data":{"rdata":"\\# 0"}}]}""",
            DnsJsonRenderer.Render(DnsMessageDecoder.Decode(message).Value!));
    }

    [TestMethod]
    [DataRow("A", "A", """{"address":"93.184.216.34"}""")]
    [DataRow("AAAA", "AAAA", """{"address":"2001:db8::1"}""")]
    [DataRow("NS", "NS", """{"nsdname":"ns1.example.com."}""")]
    [DataRow("CNAME", "CNAME", """{"cname":"www.example.net."}""")]
    [DataRow("PTR", "PTR", """{"ptrdname":"a\\.b\\032.example."}""")]
    [DataRow("MX", "MX", """{"preference":10,"exchange":"mail.example.com."}""")]
    [DataRow("TXT", "TXT", """{"txt":["v=spf1 -all","a\\\"b\\\\","\\255"]}""")]
    [DataRow("SOA", "SOA", """{"mname":"ns1.example.com.","rname":"hostmaster.example.com.","serial":2024010101,"refresh":7200,"retry":3600,"expire":1209600,"minimum":300}""")]
    [DataRow("SRV", "SRV", """{"priority":10,"weight":20,"port":443,"target":"sip.example.com."}""")]
    [DataRow("CAA", "CAA", """{"flags":128,"tag":"issue","value":["letsencrypt.org"]}""")]
    [DataRow("TYPE65", "TYPE65", """{"rdata":"\\# 2 01AB"}""")]
    [DataRow("TYPE99", "TYPE99", """{"rdata":"\\# 0"}""")]
    public void Render_EachRecordType_WritesItsFieldsByName(string sample, string type, string data)
    {
        string json = DnsJsonRenderer.Render(DnsSampleRecords.Decode(sample));

        Assert.AreEqual(
            """{"id":4660,"opcode":"QUERY","status":"NOERROR","flags":["qr","rd","ra"],"question":[{"name":"example.com.","type":"A","class":"IN"}],"""
            + $$"""
              "answer":[{"name":"example.com.","type":"{{type}}","class":"IN","ttl":300,"data":{{data}}}],"authority":[],"additional":[]}
              """,
            json);
    }
}
