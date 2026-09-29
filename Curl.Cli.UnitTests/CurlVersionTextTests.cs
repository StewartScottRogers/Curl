namespace Curl.Cli;

/// <summary>
/// Pins the four <c>-V</c> / <c>--version</c> lines per platform, as ADR-0021 decides them, with the
/// <c>Protocols:</c> and <c>Features:</c> lists kept current by its Decision 6: <c>http</c>,
/// <c>https</c>, <c>brotli</c> and <c>libz</c> joined when the HTTP handler was registered. This test
/// is the current state of those lists; update it with every handler or feature that lands.
/// </summary>
[TestClass]
public sealed class CurlVersionTextTests
{
    private const string ReleaseDate = "Release-Date: 2026-06-24";

    private const string Protocols = "Protocols: dict file ftp ftps gopher gophers http https imap imaps ldap ldaps mqtt mqtts pop3 pop3s rtsp smtp smtps telnet tftp ws wss";

    private const string Features = "Features: AsynchDNS brotli GSS-API HTTP2 IPv6 Kerberos Largefile libz NTLM SPNEGO SSL";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Lines_Windows_NamesTheMingwTripleAndSchannel(bool isMacOS)
    {
        IReadOnlyList<string> lines = CurlVersionText.Lines(isWindows: true, isMacOS);

        CollectionAssert.AreEqual(
            new[] { "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel", ReleaseDate, Protocols, Features },
            lines.ToArray());
    }

    [TestMethod]
    public void Lines_Linux_NamesTheGnuTripleAndOpenSsl()
    {
        IReadOnlyList<string> lines = CurlVersionText.Lines(isWindows: false, isMacOS: false);

        CollectionAssert.AreEqual(
            new[] { "curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 OpenSSL", ReleaseDate, Protocols, Features },
            lines.ToArray());
    }

    [TestMethod]
    public void Lines_MacOS_NamesTheAppleTripleAndSecureTransport()
    {
        IReadOnlyList<string> lines = CurlVersionText.Lines(isWindows: false, isMacOS: true);

        CollectionAssert.AreEqual(
            new[] { "curl 8.21.0 (aarch64-apple-darwin25.0.0) libcurl/8.21.0 SecureTransport", ReleaseDate, Protocols, Features },
            lines.ToArray());
    }

    [TestMethod]
    public void Lines_Windows_JoinedWithCrlf_AreTheBytesTheAdrRecords()
    {
        string text = string.Concat(CurlVersionText.Lines(isWindows: true, isMacOS: false).Select(line => line + "\r\n"));

        Assert.AreEqual(
            "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel\r\n"
            + "Release-Date: 2026-06-24\r\n"
            + "Protocols: dict file ftp ftps gopher gophers http https imap imaps ldap ldaps mqtt mqtts pop3 pop3s rtsp smtp smtps telnet tftp ws wss\r\n"
            + "Features: AsynchDNS brotli GSS-API HTTP2 IPv6 Kerberos Largefile libz NTLM SPNEGO SSL\r\n",
            text);
    }
}
