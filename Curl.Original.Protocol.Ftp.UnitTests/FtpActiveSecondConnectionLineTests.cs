using System.Net;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins the active-mode <c>-v</c> line <c>Established 2nd connection to</c> against curl 8.22.0
/// (Schannel build, BL-3935): it names the URL host, bracketed when IPv6, not the server's data address.
/// </summary>
[TestClass]
public sealed class FtpActiveSecondConnectionLineTests
{
    [TestMethod]
    public void SecondConnectionEstablished_Ipv6UrlHostWithIpv4DataAddress_NamesTheBracketedUrlHost()
    {
        string line = FtpTransferMessages.SecondConnectionEstablished(
            "::1",
            new IPEndPoint(IPAddress.Loopback, 55399),
            new IPEndPoint(IPAddress.Loopback, 55398));

        Assert.AreEqual("Established 2nd connection to [::1] (127.0.0.1 port 55399) from 127.0.0.1 port 55398 ", line);
    }

    [TestMethod]
    public void SecondConnectionEstablished_Ipv4UrlHost_NamesTheUrlHostUnbracketed()
    {
        string line = FtpTransferMessages.SecondConnectionEstablished(
            "localhost",
            new IPEndPoint(IPAddress.Loopback, 55399),
            new IPEndPoint(IPAddress.Loopback, 55398));

        Assert.AreEqual("Established 2nd connection to localhost (127.0.0.1 port 55399) from 127.0.0.1 port 55398 ", line);
    }
}
