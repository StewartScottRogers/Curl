using System.Security.Authentication;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins when the provider reports TLS 1.3 session tickets (BL-1089, ADR-0306): only the
/// Schannel build after a TLS 1.3 handshake follows the records, and whatever it reports is a
/// received <c>NewSessionTicket</c>, the bytes the server sent arriving intact either way.
/// Whether the test server sends a ticket is the platform's choice, so
/// <see cref="SessionTicketRecordDetectorTests" /> pins the counting.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    [TestMethod]
    public async Task AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsOnlyReceivedTicketsAndKeepsTheData()
    {
        await AssertTls13IsAvailableAsync();

        var (messages, echoed) = await EchoOverAsync(matchesSchannelBuild: true, SslProtocols.Tls13);

        Assert.AreEqual("hello", echoed);
        Assert.IsTrue(messages.TrueForAll(message => !message.Sent && message.Bytes.Span[0] == 4));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_SchannelBuildOverTls12_ReportsNoTicket()
    {
        var (messages, echoed) = await EchoOverAsync(matchesSchannelBuild: true, SslProtocols.Tls12);

        Assert.AreEqual("hello", echoed);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_OpenSslBuildOverTls13_ReportsNoTicket()
    {
        await AssertTls13IsAvailableAsync();

        var (messages, echoed) = await EchoOverAsync(matchesSchannelBuild: false, SslProtocols.Tls13);

        Assert.AreEqual("hello", echoed);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public void FollowTicketRecordsAfterHandshake_Tls12_DropsTheDetector()
    {
        var transport = new ConnectionStream(new StreamConnection(new MemoryStream(), ServerEndPoint)) { TicketRecords = new SessionTicketRecordDetector() };

        Assert.IsNull(SslStreamTlsProvider.FollowTicketRecordsAfterHandshake(transport, SslProtocols.Tls12));
        Assert.IsNull(transport.TicketRecords);
    }

    [TestMethod]
    public void FollowTicketRecordsAfterHandshake_Tls13_KeepsTheDetector()
    {
        var detector = new SessionTicketRecordDetector();
        var transport = new ConnectionStream(new StreamConnection(new MemoryStream(), ServerEndPoint)) { TicketRecords = detector };

        Assert.AreSame(detector, SslStreamTlsProvider.FollowTicketRecordsAfterHandshake(transport, SslProtocols.Tls13));
    }

    private static async Task<(List<TlsMessageEvent> Messages, string Echoed)> EchoOverAsync(bool matchesSchannelBuild, SslProtocols serverProtocols)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, serverProtocols);
        var events = new RecordingTransferEvents();
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true, MinimumVersion: serverProtocols == SslProtocols.Tls13 ? TlsVersion.Tls13 : TlsVersion.Tls12), matchesSchannelBuild);

        var result = await provider.AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        Assert.IsNotNull(result.Connection, result.ErrorMessage);
        await using var connection = result.Connection;
        await connection.WriteAsync("hello"u8.ToArray(), CancellationToken.None);
        var echoed = Encoding.ASCII.GetString(await ReadExactlyAsync(connection, 5));

        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
        return (events.TlsMessages, echoed);
    }
}
