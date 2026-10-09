using Curl.Testing;

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

    private const string Protocols = "Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smb smbs smtp smtps telnet tftp ws wss";

    private const string Features = "Features: alt-svc AsynchDNS brotli ECH GSS-API HSTS HTTP2 HTTP3 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL threadsafe TLS-SRP UnixSockets zstd";

    private const string WindowsFeatures = "Features: alt-svc AsynchDNS brotli HSTS HTTP3 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe TLS-SRP UnixSockets zstd";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Lines_Windows_NamesTheMingwTripleAndSchannel(bool isMacOS)
    {
        IReadOnlyList<string> lines = Lines(isWindows: true, isMacOS);

        AssertLines(["curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel", ReleaseDate, Protocols, WindowsFeatures], lines);
        CollectionAssert.AreEqual(
            new[] { "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel", ReleaseDate, Protocols, WindowsFeatures },
            lines.ToArray());
    }

    [TestMethod]
    public void Lines_Linux_NamesTheGnuTripleAndOpenSsl()
    {
        IReadOnlyList<string> lines = Lines(isWindows: false, isMacOS: false);

        AssertLines(["curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 OpenSSL", ReleaseDate, Protocols, Features], lines);
        CollectionAssert.AreEqual(
            new[] { "curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 OpenSSL", ReleaseDate, Protocols, Features },
            lines.ToArray());
    }

    [TestMethod]
    public void Lines_MacOS_NamesTheAppleTripleAndSecureTransport()
    {
        IReadOnlyList<string> lines = Lines(isWindows: false, isMacOS: true);

        AssertLines(["curl 8.21.0 (aarch64-apple-darwin25.0.0) libcurl/8.21.0 SecureTransport", ReleaseDate, Protocols, Features], lines);
        CollectionAssert.AreEqual(
            new[] { "curl 8.21.0 (aarch64-apple-darwin25.0.0) libcurl/8.21.0 SecureTransport", ReleaseDate, Protocols, Features },
            lines.ToArray());
    }

    [TestMethod]
    public void Lines_Windows_JoinedWithCrlf_AreTheBytesTheAdrRecords()
    {
        string text = string.Concat(Lines(isWindows: true, isMacOS: false).Select(line => line + "\r\n"));
        Diagnostics.Bytes("text", System.Text.Encoding.ASCII.GetBytes(text));

        string expected =
            "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel\r\n"
            + "Release-Date: 2026-06-24\r\n"
            + "Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smb smbs smtp smtps telnet tftp ws wss\r\n"
            + "Features: alt-svc AsynchDNS brotli HSTS HTTP3 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe TLS-SRP UnixSockets zstd\r\n";
        Diagnostics.Diff("text", expected, text);
        Assert.AreEqual(
            "curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel\r\n"
            + "Release-Date: 2026-06-24\r\n"
            + "Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smb smbs smtp smtps telnet tftp ws wss\r\n"
            + "Features: alt-svc AsynchDNS brotli HSTS HTTP3 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe TLS-SRP UnixSockets zstd\r\n",
            text);
    }

    /// <summary>Returns <see cref="CurlVersionText.Lines"/> for the given platform, writing the platform and the lines as diagnostics.</summary>
    private IReadOnlyList<string> Lines(bool isWindows, bool isMacOS)
    {
        Diagnostics.Arrange("is windows", isWindows);
        Diagnostics.Arrange("is macOS", isMacOS);
        IReadOnlyList<string> lines = CurlVersionText.Lines(isWindows, isMacOS);
        Diagnostics.Act("lines", CommandLineParseDiagnostics.QuoteEach(lines));
        return lines;
    }

    private void AssertLines(string[] expected, IReadOnlyList<string> actual) =>
        Diagnostics.Assert("lines", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(actual));
}
