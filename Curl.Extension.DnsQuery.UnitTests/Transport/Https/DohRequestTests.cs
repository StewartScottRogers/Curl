using System.Text;
using Curl.Extension.DnsQuery.Transport.Https;

namespace Curl.Extension.DnsQuery.UnitTests.Transport.Https;

/// <summary>Checks the HTTP/1.1 POST that carries a DoH query (RFC 8484 sections 4.1 and 6).</summary>
[TestClass]
public sealed class DohRequestTests
{
    private static readonly byte[] Query = [0x12, 0x34, 0x01, 0x00];

    [TestMethod]
    public void Encode_Rfc8484Section4_1_PostsTheQueryToDnsQueryAsADnsMessage()
    {
        byte[] request = DohRequest.Encode("dns.example", 443, Query);

        string head = "POST /dns-query HTTP/1.1\r\nHost: dns.example\r\nAccept: application/dns-message\r\n" +
            "Content-Type: application/dns-message\r\nContent-Length: 4\r\nConnection: close\r\n\r\n";
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(head).Concat(Query).ToArray(), request);
    }

    [TestMethod]
    [DataRow("dns.example", 8443, "Host: dns.example:8443\r\n")]
    [DataRow("2001:db8::1", 443, "Host: [2001:db8::1]\r\n")]
    [DataRow("2001:db8::1", 8443, "Host: [2001:db8::1]:8443\r\n")]
    public void Encode_HostHeader_BracketsAnIpv6AddressAndNamesAPortOtherThan443(string host, int port, string expected)
    {
        string head = Encoding.ASCII.GetString(DohRequest.Encode(host, port, Query));

        StringAssert.Contains(head, expected);
    }
}
