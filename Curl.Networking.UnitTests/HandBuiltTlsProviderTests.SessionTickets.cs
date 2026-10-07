using System.Text;

using Curl.Networking.Fakes;
using Curl.Networking.Fakes.Tls13Server;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Pins the TLS 1.3 session tickets the hand-built path reports (BL-1096), as
/// <see cref="SslStreamTlsProviderTests" /> pins the <see cref="System.Net.Security.SslStream" />
/// path's (BL-1089, ADR-0309): matching the Schannel build, each ticket record the server sends
/// is one received <c>NewSessionTicket</c> <see cref="TlsMessageEvent" />; matching the
/// OpenSSL build, none is reported.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    public async Task ReadAsync_SchannelBuildAfterTwoTicketRecords_ReportsOneReceivedNewSessionTicketPerRecord()
    {
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("options", "Insecure: true, MinimumVersion: Tls13");
        Diagnostics.Arrange("server", "TLS 1.3 sends 2 NewSessionTicket records, then 'hello'");

        List<TlsMessageEvent> messages;
        string read;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (messages, read) = await ReadAfterTicketRecordsAsync(SchannelBuild, ticketRecords: 2);
        }

        Diagnostics.Act("text read", read);
        Diagnostics.Act("TLS messages reported", messages.Count);
        Diagnostics.Assert("text read", "hello", read);
        Assert.AreEqual("hello", read);
        Diagnostics.Assert("TLS messages reported", 2, messages.Count);
        Assert.HasCount(2, messages);
        Diagnostics.Assert(
            "every message is a received NewSessionTicket handshake record",
            true,
            messages.TrueForAll(message => !message.Sent && message.ContentType == Curl.Protocol.Abstractions.TlsContentType.Handshake && message.ProtocolVersion == 0x0304 && message.Bytes.Span[0] == 4));
        Assert.IsTrue(messages.TrueForAll(message =>
            !message.Sent && message.ContentType == Curl.Protocol.Abstractions.TlsContentType.Handshake && message.ProtocolVersion == 0x0304 && message.Bytes.Span[0] == 4));
    }

    [TestMethod]
    public async Task ReadAsync_OpenSslBuildAfterTicketRecords_ReportsNoTicket()
    {
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("options", "Insecure: true, MinimumVersion: Tls13");
        Diagnostics.Arrange("server", "TLS 1.3 sends 2 NewSessionTicket records, then 'hello'");

        List<TlsMessageEvent> messages;
        string read;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (messages, read) = await ReadAfterTicketRecordsAsync(OpenSslBuild, ticketRecords: 2);
        }

        Diagnostics.Act("text read", read);
        Diagnostics.Act("TLS messages reported", messages.Count);
        Diagnostics.Assert("text read", "hello", read);
        Assert.AreEqual("hello", read);
        Diagnostics.Assert("TLS messages reported", 0, messages.Count);
        Assert.IsEmpty(messages);
    }

    [TestMethod]
    public async Task ReadAsync_OverAStreamThatIsNotTls13_ReportsNoTicket()
    {
        var events = new RecordingTransferEvents();
        await using var connection = new HandBuiltTlsConnection(
            new MemoryStream([1, 2, 3]), new StreamConnection(new MemoryStream(), ServerEndPoint), null, "unused", clearsTls: false)
        {
            TicketEvents = events,
        };
        Diagnostics.Arrange("stream", "a MemoryStream of 3 bytes, not TLS 1.3");

        int read;
        using (Diagnostics.Phase("read"))
        {
            read = await connection.ReadAsync(new byte[8], CancellationToken.None);
        }

        Diagnostics.Act("bytes read", read);
        Diagnostics.Act("TLS messages reported", events.TlsMessages.Count);
        Diagnostics.Assert("bytes read", 3, read);
        Assert.AreEqual(3, read);
        Diagnostics.Assert("TLS messages reported", 0, events.TlsMessages.Count);
        Assert.IsEmpty(events.TlsMessages);
    }

    // Handshakes with the in-memory TLS 1.3 server, which then sends each ticket in its own
    // record and "hello"; returns the TLS messages reported and the text the client read.
    private static async Task<(List<TlsMessageEvent> Messages, string Read)> ReadAfterTicketRecordsAsync(bool matchesSchannelBuild, int ticketRecords)
    {
        using var pki = new OcspTestPki();
        var (client, serverStream) = InMemoryDuplexStream.CreatePair();
        var records = new Tls13RecordTestServer(serverStream, new Tls13TestServer(pki.LeafCredential));
        var serverTask = Task.Run(async () =>
        {
            await records.HandshakeAsync();
            for (int ticket = 0; ticket < ticketRecords; ticket++)
            {
                await records.SendAsync(Curl.Tls.TlsContentType.Handshake, new NewSessionTicket(7200, 0, [(byte)ticket], [9, 9, (byte)ticket], []).Encode());
            }

            await records.SendAsync(Curl.Tls.TlsContentType.ApplicationData, Encoding.ASCII.GetBytes("hello"));
        });
        var events = new RecordingTransferEvents();
        var result = await Provider(new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13), matchesSchannelBuild)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await serverTask;

        byte[] buffer = new byte[5];
        int read = await result.Connection!.ReadAsync(buffer, CancellationToken.None);
        await result.Connection.DisposeAsync();
        return (events.TlsMessages, Encoding.ASCII.GetString(buffer, 0, read));
    }
}
