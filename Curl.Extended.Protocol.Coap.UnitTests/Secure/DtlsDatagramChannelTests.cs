using System.Text;
using Curl.Extended.Protocol.Coap.Client;
using Curl.Extended.Protocol.Coap.Dtls.Handshake;
using Curl.Extended.Protocol.Coap.Dtls.TestServer;
using Curl.Extended.Protocol.Coap.Dtls.Wire;
using Curl.Extended.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;
using TlsContentType = Curl.Tls.TlsContentType;

namespace Curl.Extended.Protocol.Coap.Secure;

/// <summary>
/// Checks <c>coaps://</c> against RFC 7252 section 9.1: the CoAP handler runs a DTLS 1.2 handshake with the
/// in-memory DTLS server over a fake datagram connector, to port 5684 by default, verifies the server's
/// certificate through the TLS provider (section 9.1.3.3) and carries each CoAP message as one protected
/// application data record.
/// </summary>
[TestClass]
public sealed class DtlsDatagramChannelTests
{
    private static readonly byte[] Hello = Encoding.ASCII.GetBytes("hello");

    private static readonly byte[] Representation = [.. Enumerable.Range(0, 2500).Select(index => (byte)(index * 7))];

    [TestMethod]
    public async Task ExecuteAsync_Rfc7252Section9_1_ACoapsGet_RunsOverDtlsToPort5684()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa) { AnswerApplicationData = request => Piggybacked(request, CoapCode.Content, [], Hello) };
        DtlsTestServerChannel channel = new(server);
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Opened(channel));
        var output = new MemoryStream();

        TransferResult result = await Handler(connector, insecure: true).ExecuteAsync(Context("coaps://Example.COM/a", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Hello, output.ToArray());
        Assert.AreEqual(("Example.COM", 5684), connector.Opened.Single());
        Assert.IsTrue(server.HandshakeComplete);
        CoapMessage request = CoapMessageEncoding.Decode(server.ReceivedApplicationData.Single());
        Assert.AreEqual(CoapMessageType.Confirmable, request.Type);
        Assert.AreEqual(CoapCode.Get, request.Code);
        DtlsRecordHeader sent = DtlsRecordHeader.Decode(channel.Sent.Last());
        Assert.AreEqual(TlsContentType.ApplicationData, sent.ContentType);
        Assert.AreEqual((ushort)1, sent.Epoch);
    }

    [TestMethod]
    public async Task ExecuteAsync_Rfc7252Section9_1_ACoapsUrlWithAPort_OpensThatPort()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa) { AnswerApplicationData = request => Piggybacked(request, CoapCode.Content, [], Hello) };
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Opened(new DtlsTestServerChannel(server)));

        TransferResult result = await Handler(connector, insecure: true).ExecuteAsync(Context("coaps://h:7000/a", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(("h", 7000), connector.Opened.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_Rfc7252Section9_1_ACoapsBlockwiseGet_FetchesEveryBlockOverDtls()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa) { AnswerApplicationData = BlockOfRepresentation };
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Opened(new DtlsTestServerChannel(server)));
        var output = new MemoryStream();

        TransferResult result = await Handler(connector, insecure: true).ExecuteAsync(Context("coaps://h/large", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Representation, output.ToArray());
        Assert.HasCount(3, server.ReceivedApplicationData);
    }

    [TestMethod]
    public async Task ExecuteAsync_Rfc7252Section9_1_3_3_AnUntrustedCertificate_FailsWithExit60()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa) { AnswerApplicationData = request => Piggybacked(request, CoapCode.Content, [], Hello) };
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Opened(new DtlsTestServerChannel(server)));
        var output = new MemoryStream();

        TransferResult result = await Handler(connector, insecure: false).ExecuteAsync(Context("coaps://h/a", output));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual("SSL certificate problem: self-signed certificate", result.ErrorMessage);
        Assert.IsEmpty(server.ReceivedApplicationData);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_Rfc7252Section9_1_3_3_AnUntrustedCertificateUnderInsecure_Succeeds()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Rsa) { AnswerApplicationData = request => Piggybacked(request, CoapCode.Content, [], Hello) };
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Opened(new DtlsTestServerChannel(server)));
        var output = new MemoryStream();

        TransferResult result = await Handler(connector, insecure: true).ExecuteAsync(Context("coaps://h/a", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Hello, output.ToArray());
    }

    private static CoapProtocolHandler Handler(ScriptedDatagramConnector connector, bool insecure) =>
        new(connector, length => new byte[length], new TrustSettingTlsProvider(insecure));

    private static TransferContext Context(string url, Stream output) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        TimeProvider = new SilentTimeProvider(),
    };

    private static byte[] BlockOfRepresentation(byte[] datagram)
    {
        CoapMessage request = CoapMessageEncoding.Decode(datagram);
        uint number = request.Options.Where(option => option.Number == CoapOptionNumber.Block2).Select(option => CoapBlockValue.Decode(option.Value).Number).SingleOrDefault();
        int offset = (int)number * 1024;
        int length = Math.Min(1024, Representation.Length - offset);
        bool more = offset + length < Representation.Length;
        return Piggybacked(datagram, CoapCode.Content, [new(CoapOptionNumber.Block2, new CoapBlockValue(number, more, 6).Encode())], Representation[offset..(offset + length)]);
    }

    private static byte[] Piggybacked(byte[] datagram, CoapCode code, CoapOption[] options, byte[] payload)
    {
        CoapMessage request = CoapMessageEncoding.Decode(datagram);
        return CoapMessageEncoding.Encode(new CoapMessage(CoapMessageType.Acknowledgement, code, request.MessageId, request.Token, options, payload));
    }
}
