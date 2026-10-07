using System.Net;
using System.Text;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the PROXY protocol v1 line <see cref="HaproxyProtocolHeader" /> builds against curl 8.21.0
/// (mingw, Schannel), measured on 2026-09-30 with <c>Record-CurlExchange.ps1</c> (BL-616 Notes).
/// </summary>
[TestClass]
public sealed class HaproxyProtocolHeaderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Build_OverIPv4_WritesTcp4WithTheLocalAndRemoteEnds()
    {
        // --haproxy-protocol --interface 127.0.0.1 http://127.0.0.2:48640/ -> PROXY TCP4 127.0.0.1 127.0.0.2 50276 48640
        var local = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 50276);
        var remote = new IPEndPoint(IPAddress.Parse("127.0.0.2"), 48640);
        ArrangeEnds(null, local, remote);

        var line = new HaproxyProtocolHeader(null).Build(local, remote);

        AssertLine("PROXY TCP4 127.0.0.1 127.0.0.2 50276 48640\r\n", line);
        Assert.AreEqual("PROXY TCP4 127.0.0.1 127.0.0.2 50276 48640\r\n", Encoding.ASCII.GetString(line));
    }

    [TestMethod]
    public void Build_OverIPv6_WritesTcp6WithTheAddressesAsInetNtopWritesThem()
    {
        // --haproxy-protocol http://[::1]:48617/ -> PROXY TCP6 ::1 ::1 52934 48617
        var local = new IPEndPoint(IPAddress.IPv6Loopback, 52934);
        var remote = new IPEndPoint(IPAddress.IPv6Loopback, 48617);
        ArrangeEnds(null, local, remote);

        var line = new HaproxyProtocolHeader(null).Build(local, remote);

        AssertLine("PROXY TCP6 ::1 ::1 52934 48617\r\n", line);
        Assert.AreEqual("PROXY TCP6 ::1 ::1 52934 48617\r\n", Encoding.ASCII.GetString(line));
    }

    [TestMethod]
    public void Build_OverIPv6WithAScopeId_LeavesTheScopeOut()
    {
        var local = new IPAddress(IPAddress.Parse("fe80::1").GetAddressBytes(), 7);
        var remote = new IPAddress(IPAddress.Parse("fe80::2").GetAddressBytes(), 7);
        ArrangeEnds(null, new IPEndPoint(local, 1000), new IPEndPoint(remote, 80));

        var line = new HaproxyProtocolHeader(null).Build(new IPEndPoint(local, 1000), new IPEndPoint(remote, 80));

        AssertLine("PROXY TCP6 fe80::1 fe80::2 1000 80\r\n", line);
        Assert.AreEqual("PROXY TCP6 fe80::1 fe80::2 1000 80\r\n", Encoding.ASCII.GetString(line));
    }

    [TestMethod]
    public void Build_WithoutALocalEnd_WritesTheUnspecifiedAddressAndPortZero()
    {
        var remote = new IPEndPoint(IPAddress.Parse("192.0.2.1"), 80);
        ArrangeEnds(null, null, remote);

        var line = new HaproxyProtocolHeader(null).Build(null, remote);

        AssertLine("PROXY TCP4 0.0.0.0 192.0.2.1 0 80\r\n", line);
        Assert.AreEqual("PROXY TCP4 0.0.0.0 192.0.2.1 0 80\r\n", Encoding.ASCII.GetString(line));
    }

    [TestMethod]
    [DataRow("1.2.3.4", "PROXY TCP4 1.2.3.4 1.2.3.4 52946 48618\r\n")]
    [DataRow("9.9.9.9", "PROXY TCP4 9.9.9.9 9.9.9.9 52946 48618\r\n")]
    [DataRow("2001:db8::1", "PROXY TCP6 2001:db8::1 2001:db8::1 52946 48618\r\n")]
    [DataRow("not-an-ip", "PROXY TCP6 not-an-ip not-an-ip 52946 48618\r\n")]
    [DataRow("1.2.3", "PROXY TCP6 1.2.3 1.2.3 52946 48618\r\n")]
    [DataRow("01.2.3.4", "PROXY TCP6 01.2.3.4 01.2.3.4 52946 48618\r\n")]
    [DataRow("::ffff:1.2.3.4", "PROXY TCP6 ::ffff:1.2.3.4 ::ffff:1.2.3.4 52946 48618\r\n")]
    [DataRow("[::1]", "PROXY TCP6 [::1] [::1] 52946 48618\r\n")]
    public void Build_WithAClientIp_WritesItVerbatimForBothAddresses(string clientIp, string expected)
    {
        // --haproxy-clientip <ip> http://127.0.0.1:<P>/ -> the value for source and destination, TCP4 only for a dotted quad.
        var local = new IPEndPoint(IPAddress.Loopback, 52946);
        var remote = new IPEndPoint(IPAddress.Loopback, 48618);
        ArrangeEnds(clientIp, local, remote);

        var line = new HaproxyProtocolHeader(clientIp).Build(local, remote);

        AssertLine(expected, line);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(line));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("1.2.3.4")]
    public void Build_OverAUnixSocket_WritesProxyUnknown(string? clientIp)
    {
        // --haproxy-protocol (or --haproxy-clientip 1.2.3.4) --unix-socket <path> http://localhost/ -> PROXY UNKNOWN
        ArrangeEnds(clientIp, null, null);

        var line = new HaproxyProtocolHeader(clientIp).Build(null, null);

        AssertLine("PROXY UNKNOWN\r\n", line);
        Assert.AreEqual("PROXY UNKNOWN\r\n", Encoding.ASCII.GetString(line));
    }

    [TestMethod]
    [DataRow("0.0.0.0", true)]
    [DataRow("255.255.255.255", true)]
    [DataRow("256.1.1.1", false)]
    [DataRow("1.2.3.4.5", false)]
    [DataRow("1..3.4", false)]
    [DataRow("1.2.3.1234", false)]
    [DataRow("1.2.3.a", false)]
    [DataRow("1.2.3.00", false)]
    public void IsDottedQuad_ReadsAnAddressAsInetPtonDoes(string text, bool expected)
    {
        Diagnostics.Arrange("text", text);

        var actual = HaproxyProtocolHeader.IsDottedQuad(text);

        Diagnostics.Act("is dotted quad", actual);
        Diagnostics.Assert("is dotted quad", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private void ArrangeEnds(string? clientIp, IPEndPoint? local, IPEndPoint? remote)
    {
        Diagnostics.Arrange("client IP", clientIp ?? "none");
        Diagnostics.Arrange("local end", local?.ToString() ?? "none");
        Diagnostics.Arrange("remote end", remote?.ToString() ?? "none");
    }

    private void AssertLine(string expected, byte[] line)
    {
        Diagnostics.Bytes("line", line);
        Diagnostics.Act("line", Encoding.ASCII.GetString(line).TrimEnd('\r', '\n'));
        Diagnostics.Diff("line", Encoding.ASCII.GetBytes(expected), line);
    }
}
