using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins CGR-020 (BL-3832): a CONNECT request's <c>--proxy-header</c> holding the U+DC80-U+DCFF
/// characters that stand for header-file bytes UTF-8 could not read sends those bytes.
/// </summary>
[TestClass]
public sealed class Cgr020TunnelTests
{
    [TestMethod]
    public void BuildConnectRequest_ProxyHeaderWithEscapedBytes_SendsThoseBytes()
    {
        var proxy = new ProxyEndpoint(ProxyKind.Https, "127.0.0.1", 20600, null);
        var options = new HttpProxyTunnelOptions(null, Encoding.UTF8) { CommandLineTextEncoding = Encoding.UTF8, ProxyHeaders = ["X-C: \uDCFF\uDC80\uDCC3(𐂀"] };

        byte[] request = HttpProxyTunnel.BuildConnectRequest("example.com", 443, proxy, options, null);

        byte[] expected = [.. "X-C: "u8, 0xFF, 0x80, 0xC3, (byte)'(', 0xF0, 0x90, 0x82, 0x80, (byte)'\r', (byte)'\n'];
        int at = request.AsSpan().IndexOf("X-C: "u8);
        CollectionAssert.AreEqual(expected, request.AsSpan(at, expected.Length).ToArray());
    }
}
