using System.Net;
using System.Security.Authentication;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The OpenSSL build's handshake message trace (BL-4009, ADR-0982): one
/// <see cref="TlsMessageEvent" /> per handshake message and change_cipher_spec the hand-built
/// client sends or receives, in wire order, and the negotiated group and peer signature type
/// on the <see cref="TlsHandshakeEvent" />.
/// </summary>
public sealed partial class HandBuiltTlsProviderTests
{
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls13);

        var result = await HandshakeWithEchoServerAsync(new TlsClientOptions(Insecure: true), SslProtocols.Tls13, OpenSslBuild, events);
        await using var connection = result.Connection!;
        var handshakeMessages = HandshakeMessagesOf(events);
        Diagnostics.Act("handshake messages", string.Join(" ", handshakeMessages));
        Diagnostics.Act("change_cipher_spec bytes", string.Join(" ", ChangeCipherSpecBytesOf(events)));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        string[] expected = ["OUT 0304 1", "IN 0304 2", "IN 0304 8", "IN 0304 11", "IN 0304 15", "IN 0304 20", "OUT 0304 20"];
        CollectionAssert.AreEqual(expected, handshakeMessages);
        AssertEachHandshakeMessageIsWhole(events);
        Assert.IsTrue(ChangeCipherSpecBytesOf(events).All(bytes => bytes == "01"));
        Assert.IsNotNull(events.Handshakes[0].NegotiatedGroupName);
        Assert.AreEqual("RSASSA-PSS", events.Handshakes[0].PeerSignatureTypeName);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_Tls12InTheOpenSslBuild_ReportsTheHelloAsOfferedAndTheRestAsNegotiated()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("offered", "TLS 1.3 and TLS 1.2");
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);

        var result = await HandshakeWithEchoServerAsync(new TlsClientOptions(Insecure: true), SslProtocols.Tls12, OpenSslBuild, events);
        await using var connection = result.Connection!;
        var messages = events.TlsMessages.Select(Describe).ToArray();
        Diagnostics.Act("messages", string.Join(" ", messages));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        string[] expected = ["OUT 0304 1", "IN 0303 2", "IN 0303 11", "IN 0303 12", "IN 0303 14", "OUT 0303 16", "OUT 0303 CCS", "OUT 0303 20"];
        CollectionAssert.AreEqual(expected, messages.Take(expected.Length).ToArray());
        Assert.AreEqual("IN 0303 20", messages[^1]);
        AssertEachHandshakeMessageIsWhole(events);
        Assert.IsNotNull(events.Handshakes[0].NegotiatedGroupName);
        Assert.IsNotNull(events.Handshakes[0].PeerSignatureTypeName);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_InTheSchannelBuild_ReportsNoHandshakeMessage()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "Schannel");

        var result = await HandshakeWithEchoServerAsync(Tls12Only(new TlsClientOptions(Insecure: true)), SslProtocols.Tls12, SchannelBuild, events);
        await using var connection = result.Connection!;
        Diagnostics.Act("messages", events.TlsMessages.Count);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsEmpty(events.TlsMessages);
    }

    private static async Task<ConnectResult> HandshakeWithEchoServerAsync(TlsClientOptions options, SslProtocols serverProtocols, bool matchesSchannelBuild, RecordingTransferEvents events)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        _ = RunEchoServerAsync(server, serverProtocols);
        return await Provider(options, matchesSchannelBuild)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint, new IPEndPoint(IPAddress.Loopback, 50000)), CertificateHost, events, false, Http11, CancellationToken.None);
    }

    private static string Describe(TlsMessageEvent message) =>
        $"{(message.Sent ? "OUT" : "IN")} {message.ProtocolVersion:x4} {(message.ContentType == TlsContentType.Handshake ? message.Bytes.Span[0].ToString(System.Globalization.CultureInfo.InvariantCulture) : "CCS")}";

    private static string[] HandshakeMessagesOf(RecordingTransferEvents events) =>
        [.. events.TlsMessages.Where(message => message.ContentType == TlsContentType.Handshake).Select(Describe)];

    private static string[] ChangeCipherSpecBytesOf(RecordingTransferEvents events) =>
        [.. events.TlsMessages.Where(message => message.ContentType == TlsContentType.ChangeCipherSpec).Select(message => Convert.ToHexString(message.Bytes.Span))];

    // Each handshake message is reported whole: its four-byte header's length is the rest.
    private static void AssertEachHandshakeMessageIsWhole(RecordingTransferEvents events)
    {
        foreach (var message in events.TlsMessages.Where(message => message.ContentType == TlsContentType.Handshake))
        {
            var bytes = message.Bytes.Span;
            Assert.AreEqual((bytes[1] << 16) | (bytes[2] << 8) | bytes[3], bytes.Length - 4);
        }
    }
}
