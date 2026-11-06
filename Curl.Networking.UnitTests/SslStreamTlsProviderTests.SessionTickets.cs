using System.Security.Authentication;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins when the provider reports TLS 1.3 session tickets (BL-1089, ADR-0309): only the
/// Schannel build after a TLS 1.3 handshake follows the records, and whatever it reports is a
/// received <c>NewSessionTicket</c>, the bytes the server sent arriving intact either way.
/// Whether the test server sends a ticket is the platform's choice, so
/// <see cref="SessionTicketRecordDetectorTests" /> pins the counting.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    // Windows' Schannel test server sends a NewSessionTicket after a TLS 1.3 handshake, so there
    // the provider must report at least one (AF-0123): an empty list would mean it stopped reporting.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsTheReceivedTicketsAndKeepsTheData()
    {
        await AssertTls13IsAvailableAsync();

        var (messages, echoed) = await EchoOverAsync(matchesSchannelBuild: true, SslProtocols.Tls13);

        Diagnostics.Assert("echoed", "hello", echoed);
        Diagnostics.Assert("at least one ticket reported", true, messages.Count > 0);
        Diagnostics.Assert("all received NewSessionTicket", true, messages.TrueForAll(message => !message.Sent && message.Bytes.Span[0] == 4));
        Assert.AreEqual("hello", echoed);
        Assert.IsNotEmpty(messages);
        Assert.IsTrue(messages.TrueForAll(message => !message.Sent && message.Bytes.Span[0] == 4));
    }

    // Elsewhere whether the test server sends a ticket is the platform's choice, so an empty list
    // passes: the test pins only that nothing but received tickets is reported.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsNothingButReceivedTicketsAndKeepsTheData()
    {
        await AssertTls13IsAvailableAsync();

        var (messages, echoed) = await EchoOverAsync(matchesSchannelBuild: true, SslProtocols.Tls13);

        Diagnostics.Assert("echoed", "hello", echoed);
        Diagnostics.Assert("all received NewSessionTicket", true, messages.TrueForAll(message => !message.Sent && message.Bytes.Span[0] == 4));
        Assert.AreEqual("hello", echoed);
        Assert.IsTrue(messages.TrueForAll(message => !message.Sent && message.Bytes.Span[0] == 4));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_SchannelBuildOverTls12_ReportsNoTicket()
    {
        var (messages, echoed) = await EchoOverAsync(matchesSchannelBuild: true, SslProtocols.Tls12);

        Diagnostics.Assert("echoed", "hello", echoed);
        Diagnostics.Assert("TLS message count", 0, messages.Count);
        Assert.AreEqual("hello", echoed);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_OpenSslBuildOverTls13_ReportsNoTicket()
    {
        await AssertTls13IsAvailableAsync();

        var (messages, echoed) = await EchoOverAsync(matchesSchannelBuild: false, SslProtocols.Tls13);

        Diagnostics.Assert("echoed", "hello", echoed);
        Diagnostics.Assert("TLS message count", 0, messages.Count);
        Assert.AreEqual("hello", echoed);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public void FollowTicketRecordsAfterHandshake_Tls12_DropsTheDetector()
    {
        var transport = new ConnectionStream(new StreamConnection(new MemoryStream(), ServerEndPoint)) { TicketRecords = new SessionTicketRecordDetector() };
        Diagnostics.Arrange("negotiated protocol", SslProtocols.Tls12);
        Diagnostics.Arrange("ticket detector", "set");

        var followed = SslStreamTlsProvider.FollowTicketRecordsAfterHandshake(transport, SslProtocols.Tls12);

        Diagnostics.Act("detector returned", followed is not null);
        Diagnostics.Act("detector kept on transport", transport.TicketRecords is not null);
        Diagnostics.Assert("detector kept on transport", false, transport.TicketRecords is not null);
        Assert.IsNull(followed);
        Assert.IsNull(transport.TicketRecords);
    }

    [TestMethod]
    public void FollowTicketRecordsAfterHandshake_Tls13_KeepsTheDetector()
    {
        var detector = new SessionTicketRecordDetector();
        var transport = new ConnectionStream(new StreamConnection(new MemoryStream(), ServerEndPoint)) { TicketRecords = detector };
        Diagnostics.Arrange("negotiated protocol", SslProtocols.Tls13);
        Diagnostics.Arrange("ticket detector", "set");

        var followed = SslStreamTlsProvider.FollowTicketRecordsAfterHandshake(transport, SslProtocols.Tls13);

        Diagnostics.Act("same detector returned", ReferenceEquals(detector, followed));
        Diagnostics.Assert("same detector returned", true, ReferenceEquals(detector, followed));
        Assert.AreSame(detector, followed);
    }

    /// <summary>
    /// Writes the TLS settings, build and server protocols as ARRANGE, runs the handshake inside
    /// a <c>handshake</c> PHASE, echoes <c>hello</c>, and writes the echo and the TLS messages
    /// reported as ACT. Windows' in-process Schannel now and then refuses a handshake between
    /// its own client and server under CI load (BL-2792, BL-4880), so a refused handshake is
    /// written as ACT with the server's exception and tried once more against a fresh server:
    /// these tests pin ticket reporting, which other tests' handshakes do not depend on.
    /// </summary>
    private async Task<(List<TlsMessageEvent> Messages, string Echoed)> EchoOverAsync(bool matchesSchannelBuild, SslProtocols serverProtocols)
    {
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: serverProtocols == SslProtocols.Tls13 ? TlsVersion.Tls13 : TlsVersion.Tls12);
        ArrangeOptions(options);
        Diagnostics.Arrange("build", BuildName(matchesSchannelBuild));
        Diagnostics.Arrange("server protocols", serverProtocols);

        var (result, serverTask, events) = await HandshakeWithEchoServerAsync(options, matchesSchannelBuild, serverProtocols);
        if (result.Connection is null)
        {
            Diagnostics.Act("refused handshake, server side", await ServerFailureAsync(serverTask));
            (result, serverTask, events) = await HandshakeWithEchoServerAsync(options, matchesSchannelBuild, serverProtocols);
        }

        ActResult(result);
        Assert.IsNotNull(result.Connection, result.ErrorMessage);
        await using var connection = result.Connection;
        await connection.WriteAsync("hello"u8.ToArray(), CancellationToken.None);
        var echoed = Encoding.ASCII.GetString(await ReadExactlyAsync(connection, 5));

        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
        Diagnostics.Act("echoed", echoed);
        Diagnostics.Act(
            "TLS messages",
            string.Join(", ", events.TlsMessages.Select(message => $"{(message.Sent ? "sent" : "received")} type {message.Bytes.Span[0]}")));
        return (events.TlsMessages, echoed);
    }

    /// <summary>Runs one handshake against a fresh echo server inside a <c>handshake</c> PHASE.</summary>
    private async Task<(ConnectResult Result, Task ServerTask, RecordingTransferEvents Events)> HandshakeWithEchoServerAsync(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        SslProtocols serverProtocols)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, serverProtocols);
        var events = new RecordingTransferEvents();
        var provider = new SslStreamTlsProvider(options, matchesSchannelBuild);
        using (Diagnostics.Phase("handshake"))
        {
            var result = await provider.AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
            return (result, serverTask, events);
        }
    }

    /// <summary>Returns the type and message of the exception the echo server's handshake ended with, or <c>none</c>.</summary>
    private static async Task<string> ServerFailureAsync(Task serverTask)
    {
        try
        {
            await serverTask;
            return "none";
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException or ObjectDisposedException)
        {
            return $"{exception.GetType().Name}: {exception.Message} {exception.InnerException?.Message}";
        }
    }
}
