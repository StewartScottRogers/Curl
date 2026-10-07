using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <c>--tlsuser</c> and <c>--tlspassword</c> through <see cref="HandBuiltTlsProvider" /> (ADR-0328,
/// BL-712): the ClientHello carries the <c>srp</c> extension and OpenSSL's <c>SRP</c> cipher list,
/// the verbose lines and the failures are curl's OpenSSL build's on every platform. The SRP
/// exchange itself is pinned against <c>Curl.Tls.UnitTests</c>' in-memory SRP server.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    // OpenSSL's alert records: unknown_psk_identity (115), which OpenSSL's SRP server sends for a
    // user it does not know, and decrypt_error (51), its answer to a wrong password's Finished.
    private static readonly byte[] UnknownPskIdentityAlert = [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x73];

    private static readonly byte[] DecryptErrorAlert = [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x33];

    private static TlsClientOptions SrpOptions(string? ciphers = null) =>
        new(Insecure: true, TlsUser: "alice", TlsPassword: "secret", Ciphers: ciphers);

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTlsUser_OffersTheSrpUserAndOpenSslsSrpCipherList(bool matchesSchannelBuild)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, TlsUser: alice, TlsPassword: secret");
        Diagnostics.Arrange("host", ProfileHost);

        byte[] clientHello;
        using (Diagnostics.Phase("TLS handshake"))
        {
            clientHello = await CaptureClientHelloAsync(SrpOptions(), matchesSchannelBuild, ProfileHost, Http11);
        }

        var hello = DecodeClientHello(clientHello);
        Diagnostics.Bytes("ClientHello record", clientHello);
        var srpUser = SrpExtension.Decode(ExtensionData(hello, TlsExtensionType.Srp)).Value;
        Diagnostics.Act("srp user", System.Text.Encoding.UTF8.GetString(srpUser));
        Diagnostics.Assert("srp user", "alice", System.Text.Encoding.UTF8.GetString(srpUser));
        CollectionAssert.AreEqual("alice"u8.ToArray(), SrpExtension.Decode(ExtensionData(hello, TlsExtensionType.Srp)).Value);
        ushort[] tls13Suites = [.. ProfileOf(matchesSchannelBuild).CipherSuites.Where(Tls13RecordProtection.CanProtect)];
        ushort[] expected = [.. tls13Suites, 0xc022, 0xc021, 0xc020, 0xc01f, 0xc01e, 0xc01d];
        Diagnostics.Act("cipher suites", string.Join(",", hello.CipherSuites.ToArray().Select(suite => "0x" + suite.ToString("x4"))));
        Diagnostics.Assert("cipher suites", string.Join(",", expected.Select(suite => "0x" + suite.ToString("x4"))), string.Join(",", hello.CipherSuites.ToArray().Select(suite => "0x" + suite.ToString("x4"))));
        CollectionAssert.AreEqual(expected, hello.CipherSuites.ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTlsUserAndCiphers_OffersTheCiphersInPlaceOfTheSrpList()
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, TlsUser: alice, TlsPassword: secret, MaximumVersion: Tls12, Ciphers: SRP-AES-128-CBC-SHA:ECDHE-RSA-AES128-GCM-SHA256");
        Diagnostics.Arrange("host", ProfileHost);

        byte[] clientHello;
        using (Diagnostics.Phase("TLS handshake"))
        {
            clientHello = await CaptureClientHelloAsync(Tls12Only(SrpOptions("SRP-AES-128-CBC-SHA:ECDHE-RSA-AES128-GCM-SHA256")), OpenSslBuild, ProfileHost, Http11);
        }

        var hello = DecodeClientHello(clientHello);
        Diagnostics.Bytes("ClientHello record", clientHello);
        Diagnostics.Act("cipher suites", string.Join(",", hello.CipherSuites.ToArray().Select(suite => "0x" + suite.ToString("x4"))));
        Diagnostics.Assert("offers 0xc022", false, hello.CipherSuites.ToArray().Contains((ushort)0xc022));
        Assert.DoesNotContain((ushort)0xc022, hello.CipherSuites.ToArray());
        Diagnostics.Assert("offers 0xc02f", true, hello.CipherSuites.ToArray().Contains((ushort)0xc02f));
        Assert.Contains((ushort)0xc02f, hello.CipherSuites.ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithoutTlsUser_OffersNoSrp()
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "TlsPassword: secret, no TlsUser");
        Diagnostics.Arrange("host", ProfileHost);

        byte[] clientHello;
        using (Diagnostics.Phase("TLS handshake"))
        {
            clientHello = await CaptureClientHelloAsync(new TlsClientOptions(TlsPassword: "secret"), OpenSslBuild, ProfileHost, Http11);
        }

        var hello = DecodeClientHello(clientHello);
        Diagnostics.Bytes("ClientHello record", clientHello);
        Diagnostics.Act("cipher suites", string.Join(",", hello.CipherSuites.ToArray().Select(suite => "0x" + suite.ToString("x4"))));
        Diagnostics.Assert("offers the srp extension", false, hello.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp));
        Assert.IsFalse(hello.Extensions.Any(extension => extension.Type == TlsExtensionType.Srp));
        Diagnostics.Assert("offers 0xc020", false, hello.CipherSuites.ToArray().Contains((ushort)0xc020));
        Assert.DoesNotContain((ushort)0xc020, hello.CipherSuites.ToArray());
    }

    [TestMethod]
    [DataRow(null, new[] { "Using TLS-SRP username: alice", "Setting cipher list SRP" })]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256", new[] { "Using TLS-SRP username: alice" })]
    public async Task AuthenticateAsClientAsync_WithTlsUser_ReportsOpenSslsVerboseLines(string? ciphers, string[] expected)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        await server.DisposeAsync();
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, TlsUser: alice, TlsPassword: secret");
        Diagnostics.Arrange("ciphers", ciphers ?? "(none)");
        Diagnostics.Arrange("server", "closed before it answers");
        Diagnostics.Arrange("expected verbose lines", string.Join(" | ", expected));

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(SrpOptions(ciphers), OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), ProfileHost, events, false, Http11, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("verbose lines", string.Join(" | ", events.Info.Take(expected.Length)));
        Diagnostics.Assert("verbose lines", string.Join(" | ", expected), string.Join(" | ", events.Info.Take(expected.Length)));
        CollectionAssert.AreEqual(expected, events.Info.Take(expected.Length).ToArray());
    }

    // Measured with curl 8.18.0's OpenSSL build: --tlsuser alone fails before the hello.
    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithTlsUserButNoPassword_FailsWithExit43(bool matchesSchannelBuild)
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, TlsUser: alice, no TlsPassword");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(new TlsClientOptions(Insecure: true, TlsUser: "alice"), matchesSchannelBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), ProfileHost, events, false, Http11, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("verbose lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("exit code", CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Diagnostics.Assert("error message", "failed setting SRP password", result.ErrorMessage);
        Assert.AreEqual("failed setting SRP password", result.ErrorMessage);
        Diagnostics.Assert("username line written", true, events.Info.Contains("Using TLS-SRP username: alice"));
        Assert.Contains("Using TLS-SRP username: alice", events.Info);
        Diagnostics.Assert("cipher list line written", false, events.Info.Contains("Setting cipher list SRP"));
        Assert.DoesNotContain("Setting cipher list SRP", events.Info);
    }

    // An SRP server that does not know the user (measured), one whose verifier does not match
    // the password, and a server without SRP (measured): exit 35 with the OpenSSL build's line,
    // on Windows too, since curl's Schannel build has no SRP (ADR-0151).
    [TestMethod]
    [DataRow(SchannelBuild, "UnknownPskIdentity", "TLS connect error: error:0A00045B:SSL routines::tlsv1 alert unknown psk identity")]
    [DataRow(OpenSslBuild, "UnknownPskIdentity", "TLS connect error: error:0A00045B:SSL routines::tlsv1 alert unknown psk identity")]
    [DataRow(SchannelBuild, "DecryptError", "TLS connect error: error:0A00041B:SSL routines::tlsv1 alert decrypt error")]
    [DataRow(OpenSslBuild, "DecryptError", "TLS connect error: error:0A00041B:SSL routines::tlsv1 alert decrypt error")]
    [DataRow(SchannelBuild, "HandshakeFailure", "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    [DataRow(OpenSslBuild, "HandshakeFailure", "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    public async Task AuthenticateAsClientAsync_WithTlsUserAndAServerAlert_ReportsTheOpenSslBuildsLine(bool matchesSchannelBuild, string alert, string expected)
    {
        var answer = alert switch
        {
            "UnknownPskIdentity" => UnknownPskIdentityAlert,
            "DecryptError" => DecryptErrorAlert,
            _ => HandshakeFailureAlert,
        };

        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, TlsUser: alice, TlsPassword: secret");
        Diagnostics.Arrange("server alert", alert);
        Diagnostics.Bytes("canned server answer", answer);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeWithServerAnsweringAsync(matchesSchannelBuild, answer, SrpOptions());
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Diagnostics.Assert("error message", expected, result.Result.ErrorMessage);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
    }
}
