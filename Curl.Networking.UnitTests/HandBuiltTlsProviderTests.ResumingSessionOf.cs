using System.Security.Authentication;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// A handshake that resumes the session of an earlier one on another connection, as an FTP data
/// connection resumes the control connection's (STD-220, ADR-1466).
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientResumingSessionOfAsync_AfterATls12Handshake_OffersItsSession()
    {
        var provider = new HandBuiltTlsProvider(
            new TlsClientOptions(Insecure: true), OpenSslBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance);

        var (control, resumed, lines) = await HandshakeThenResumeOverTls12Async(provider);

        Diagnostics.Assert("reusing session lines", "SSL reusing session with ALPN '-'", string.Join(" | ", lines));
        Assert.IsInstanceOfType<HandBuiltTlsConnection>(control.Connection);
        Assert.AreEqual(CurlExitCode.Ok, resumed.ExitCode, resumed.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "SSL reusing session with ALPN '-'" }, lines);
    }

    [TestMethod]
    public async Task AuthenticateAsClientResumingSessionOfAsync_UnderNoSessionId_OffersNoSession()
    {
        var provider = new HandBuiltTlsProvider(
            new TlsClientOptions(Insecure: true, NoSessionId: true), OpenSslBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance);

        var (_, resumed, lines) = await HandshakeThenResumeOverTls12Async(provider);

        Diagnostics.Assert("reusing session line count", 0, lines.Length);
        Assert.AreEqual(CurlExitCode.Ok, resumed.ExitCode, resumed.ErrorMessage);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public async Task AuthenticateAsClientResumingSessionOfAsync_AfterATls13Ticket_OffersIt()
    {
        using var pki = new OcspTestPki();
        var provider = new HandBuiltTlsProvider(
            new TlsClientOptions(Insecure: true), OpenSslBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance);
        var (client, serverStream) = InMemoryDuplexStream.CreatePair();
        var records = new Tls13RecordTestServer(serverStream, new Tls13TestServer(pki.LeafCredential));
        var serverTask = Task.Run(async () =>
        {
            await records.HandshakeAsync();
            await records.SendAsync(Curl.Tls.TlsContentType.Handshake, new NewSessionTicket(7200, 0, [1], [9, 9, 1], []).Encode());
            await records.SendAsync(Curl.Tls.TlsContentType.ApplicationData, Encoding.ASCII.GetBytes("hello"));
        });
        var control = await provider.AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, new RecordingTransferEvents(), CancellationToken.None);
        await serverTask;
        await control.Connection!.ReadAsync(new byte[5], CancellationToken.None);

        var (dataClient, dataServerStream) = InMemoryDuplexStream.CreatePair();
        var dataServerTask = Task.Run(() => new Tls13RecordTestServer(dataServerStream, new Tls13TestServer(pki.LeafCredential)).HandshakeAsync());
        var events = new RecordingTransferEvents();
        var resumed = await provider.AuthenticateAsClientResumingSessionOfAsync(
            new StreamConnection(dataClient, ServerEndPoint), control.Connection, CertificateHost, events, CancellationToken.None);
        await IgnoreServerFailureAsync(dataServerTask);

        var lines = ReusedSessionLines(events);
        Diagnostics.Assert("reusing session lines", "SSL reusing session with ALPN '-'", string.Join(" | ", lines));
        Assert.AreEqual(CurlExitCode.Ok, resumed.ExitCode, resumed.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "SSL reusing session with ALPN '-'" }, lines);
    }

    [TestMethod]
    public async Task AuthenticateAsClientResumingSessionOfAsync_OnTheSslStreamProvidersOpenSslBuild_RunsTheHandBuiltClient()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), OpenSslBuild);

        var (control, resumed, lines) = await HandshakeThenResumeOverTls12Async(provider);

        Diagnostics.Assert("reusing session line count", 1, lines.Length);
        Assert.IsInstanceOfType<HandBuiltTlsConnection>(control.Connection);
        Assert.IsInstanceOfType<HandBuiltTlsConnection>(resumed.Connection);
        Assert.HasCount(1, lines);
    }

    [TestMethod]
    public async Task AuthenticateAsClientResumingSessionOfAsync_OnTheSslStreamProvidersSchannelBuild_StaysOnSslStream()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), SchannelBuild);

        var (control, resumed, lines) = await HandshakeThenResumeOverTls12Async(provider);

        Diagnostics.Assert("reusing session line count", 0, lines.Length);
        Assert.IsInstanceOfType<SslStreamConnection>(control.Connection);
        Assert.IsInstanceOfType<SslStreamConnection>(resumed.Connection);
        Assert.IsEmpty(lines);
    }

    // Handshakes with a TLS 1.2 server, the connection as its own session source, then on a
    // second connection resuming the first's session; returns both and the second's reuse lines.
    private async Task<(ConnectResult Control, ConnectResult Resumed, string[] Lines)> HandshakeThenResumeOverTls12Async(ITlsProvider provider)
    {
        Diagnostics.Arrange("server", "SslStream, TLS 1.2, a handshake and then a second resuming the first");
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var plaintext = new StreamConnection(client, ServerEndPoint);
        var control = await provider.AuthenticateAsClientResumingSessionOfAsync(
            plaintext, plaintext, CertificateHost, new RecordingTransferEvents(), CancellationToken.None);
        Assert.AreEqual(CurlExitCode.Ok, control.ExitCode, control.ErrorMessage);

        var (dataClient, dataServer) = InMemoryDuplexStream.CreatePair();
        var dataServerTask = RunEchoServerAsync(dataServer, SslProtocols.Tls12);
        var events = new RecordingTransferEvents();
        var resumed = await provider.AuthenticateAsClientResumingSessionOfAsync(
            new StreamConnection(dataClient, ServerEndPoint), control.Connection!, CertificateHost, events, CancellationToken.None);
        return (control, resumed, ReusedSessionLines(events));
    }
}
