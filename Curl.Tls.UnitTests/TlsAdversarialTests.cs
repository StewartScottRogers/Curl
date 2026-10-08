using System.Text;
using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// Adversarial black-box tests (BL-1523, Documentation/Wiki/Adversarial-Testing.md): attack
/// the library's public decoders and handshake message reader at their length boundaries,
/// with almost-right bytes, in the partitions TLS forbids, and under repeated and parallel
/// calls. The oracle is the RFC each codec implements and its documented contract: a typed
/// alert, never an exception.
/// </summary>
[TestClass]
public sealed class TlsAdversarialTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void HandshakeReader_ThreeHeaderBytes_NeedsMoreBytes()
    {
        byte[] buffer = [(byte)HandshakeType.Finished, 0x00, 0x00];
        Diagnostics.Bytes("buffer", buffer);

        HandshakeMessageReadResult result = HandshakeMessageReader.Read(buffer);
        Diagnostics.Act("status", result.Status);

        Assert.AreEqual(HandshakeMessageReadStatus.NeedMoreBytes, result.Status);
        Assert.AreEqual(0, result.BytesConsumed);
    }

    [TestMethod]
    public void HandshakeReader_LargestUInt24LengthWithNoBody_NeedsMoreBytesWithoutThrowing()
    {
        byte[] buffer = [(byte)HandshakeType.Certificate, 0xFF, 0xFF, 0xFF, 0x00];
        Diagnostics.Bytes("buffer", buffer);

        HandshakeMessageReadResult result = HandshakeMessageReader.Read(buffer);
        Diagnostics.Act("status", result.Status);

        Assert.AreEqual(HandshakeMessageReadStatus.NeedMoreBytes, result.Status);
        Assert.IsNull(result.Message);
    }

    [TestMethod]
    public void HandshakeReader_ZeroLengthBody_CompletesWithAnEmptyBody()
    {
        byte[] buffer = [(byte)HandshakeType.EndOfEarlyData, 0x00, 0x00, 0x00];
        Diagnostics.Bytes("buffer", buffer);

        HandshakeMessageReadResult result = HandshakeMessageReader.Read(buffer);
        Diagnostics.Act("status", result.Status);

        Assert.AreEqual(HandshakeMessageReadStatus.Complete, result.Status);
        Assert.AreEqual(HandshakeMessage.HeaderLength, result.BytesConsumed);
        Assert.IsEmpty(result.Message!.Body);
    }

    [TestMethod]
    public void HandshakeReader_TwoMessagesInOneBuffer_ConsumesOnlyTheFirst()
    {
        byte[] first = new HandshakeMessage(HandshakeType.Finished, [1, 2, 3]).Encode();
        byte[] second = new HandshakeMessage(HandshakeType.KeyUpdate, [0]).Encode();
        byte[] buffer = [.. first, .. second];
        Diagnostics.Bytes("buffer", buffer);

        HandshakeMessageReadResult result = HandshakeMessageReader.Read(buffer);
        HandshakeMessageReadResult next = HandshakeMessageReader.Read(buffer.AsSpan(result.BytesConsumed));
        Diagnostics.Act("consumed", result.BytesConsumed);

        Assert.AreEqual(first.Length, result.BytesConsumed);
        Assert.AreEqual(HandshakeType.Finished, result.Message!.Type);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result.Message.Body);
        Assert.AreEqual(HandshakeType.KeyUpdate, next.Message!.Type);
        Assert.AreEqual(second.Length, next.BytesConsumed);
    }

    [TestMethod]
    public void HandshakeReader_MessageFragmentedByteByByte_CompletesOnlyOnTheLastByte()
    {
        byte[] message = new HandshakeMessage(HandshakeType.Finished, new byte[48]).Encode();

        for (int length = 0; length < message.Length; length++)
        {
            Assert.AreEqual(HandshakeMessageReadStatus.NeedMoreBytes, HandshakeMessageReader.Read(message.AsSpan(0, length)).Status, $"after {length} bytes");
        }

        Diagnostics.Act("complete at", message.Length);
        Assert.AreEqual(HandshakeMessageReadStatus.Complete, HandshakeMessageReader.Read(message).Status);
    }

    [TestMethod]
    [DataRow((byte)0x03)]
    [DataRow((byte)0xFF)]
    public void HandshakeReader_UnknownMessageType_FailsWithUnexpectedMessage(byte type)
    {
        byte[] buffer = [type, 0x00, 0x00, 0x00];
        Diagnostics.Bytes("buffer", buffer);

        HandshakeMessageReadResult result = HandshakeMessageReader.Read(buffer);
        Diagnostics.Act("alert", result.Alert);

        Assert.AreEqual(HandshakeMessageReadStatus.Failed, result.Status);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, result.Alert);
    }

    [TestMethod]
    public void Alpn_ProtocolNameOf255Bytes_RoundTrips()
    {
        string name = new('a', 255);

        TlsExtension extension = ApplicationLayerProtocolNegotiationExtension.Encode([name]);
        TlsDecodeResult<IReadOnlyList<string>> decoded = ApplicationLayerProtocolNegotiationExtension.Decode(extension.Data);
        Diagnostics.Act("decoded count", decoded.Value.Count);

        Assert.AreEqual(name, decoded.Value.Single());
    }

    [TestMethod]
    public void Alpn_ProtocolLengthPastTheListEnd_IsDecodeError()
    {
        byte[] data = [0x00, 0x03, 0x05, (byte)'h', (byte)'2'];
        Diagnostics.Bytes("data", data);

        TlsAlertDescription? alert = ApplicationLayerProtocolNegotiationExtension.Decode(data).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void Alpn_ListLengthLargerThanTheData_IsDecodeError()
    {
        byte[] data = [0xFF, 0xFF, 0x02, (byte)'h', (byte)'2'];
        Diagnostics.Bytes("data", data);

        TlsAlertDescription? alert = ApplicationLayerProtocolNegotiationExtension.Decode(data).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    [DataRow("example.com.")]
    [DataRow("192.0.2.1")]
    [DataRow("xn--bcher-kva.example")]
    public void ServerName_HostNameShapes_RoundTripByteForByte(string hostName)
    {
        TlsExtension extension = ServerNameExtension.EncodeHostName(hostName);
        Diagnostics.Bytes("encoded", extension.Data);

        TlsDecodeResult<string> decoded = ServerNameExtension.DecodeHostName(extension.Data);
        Diagnostics.Act("decoded", decoded.Value);

        Assert.AreEqual(hostName, decoded.Value);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(hostName), extension.Data[5..]);
    }

    [TestMethod]
    public void ServerName_TwoEntriesInTheList_IsDecodeError()
    {
        byte[] one = ServerNameExtension.EncodeHostName("a").Data;
        byte[] data = [0x00, 0x08, .. one[2..], .. one[2..]];
        Diagnostics.Bytes("data", data);

        TlsAlertDescription? alert = ServerNameExtension.DecodeHostName(data).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void ServerName_EmptyData_IsDecodeError()
    {
        TlsAlertDescription? alert = ServerNameExtension.DecodeHostName([]).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void ServerName_AcknowledgementWithData_IsDecodeError()
    {
        TlsAlertDescription? alert = ServerNameExtension.DecodeAcknowledgement([0x00]);
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void Certificate_DerLengthLargerThanTheList_IsDecodeError()
    {
        byte[] body = [0x00, 0x00, 0x00, 0x07, 0xFF, 0xFF, 0xFF, 0x30, 0x00, 0x00, 0x00];
        Diagnostics.Bytes("body", body);

        TlsAlertDescription? alert = CertificateMessage.Decode(body).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void Certificate_EntryWithNoExtensionBlock_IsDecodeError()
    {
        byte[] body = [0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x02, 0x30, 0x00];
        Diagnostics.Bytes("body", body);

        TlsAlertDescription? alert = CertificateMessage.Decode(body).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void Certificate_DuplicateExtensionOnOneEntry_IsIllegalParameter()
    {
        CertificateEntry entry = new(
            [0x30, 0x00],
            [new TlsExtension(TlsExtensionType.StatusRequest, [1]), new TlsExtension(TlsExtensionType.StatusRequest, [2])]);
        byte[] body = new CertificateMessage([], [entry]).Encode()[HandshakeMessage.HeaderLength..];
        Diagnostics.Bytes("body", body);

        TlsAlertDescription? alert = CertificateMessage.Decode(body).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    public void EncryptedExtensions_UnknownExtensionType_IsKeptNotRefused()
    {
        TlsExtension unknown = new((TlsExtensionType)0xFAFA, [0xAB]);
        byte[] body = new EncryptedExtensions([unknown]).Encode()[HandshakeMessage.HeaderLength..];
        Diagnostics.Bytes("body", body);

        TlsDecodeResult<EncryptedExtensions> decoded = EncryptedExtensions.Decode(body);
        Diagnostics.Act("alert", decoded.Alert);

        Assert.AreEqual((TlsExtensionType)0xFAFA, decoded.Value.Extensions.Single().Type);
        CollectionAssert.AreEqual(new byte[] { 0xAB }, decoded.Value.Extensions.Single().Data);
    }

    [TestMethod]
    public void EncryptedExtensions_DuplicateUnknownExtension_IsIllegalParameter()
    {
        TlsExtension unknown = new((TlsExtensionType)0xFAFA, []);
        byte[] body = new EncryptedExtensions([unknown, unknown]).Encode()[HandshakeMessage.HeaderLength..];
        Diagnostics.Bytes("body", body);

        TlsAlertDescription? alert = EncryptedExtensions.Decode(body).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert);
    }

    [TestMethod]
    [DataRow(new byte[] { })]
    [DataRow(new byte[] { 0x03 })]
    [DataRow(new byte[] { 0x03, 0x04, 0x00 })]
    public void SupportedVersions_SelectedNotExactlyTwoBytes_IsDecodeError(byte[] data)
    {
        TlsAlertDescription? alert = SupportedVersionsExtension.DecodeSelected(data).Alert;
        Diagnostics.Act("alert", alert);

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert);
    }

    [TestMethod]
    public void DecodeResult_ValueOfAFailure_ThrowsInvalidOperationNamingTheAlert()
    {
        TlsDecodeResult<string> failed = ServerNameExtension.DecodeHostName([]);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => failed.Value);
        Diagnostics.Act("message", exception.Message);

        StringAssert.Contains(exception.Message, nameof(TlsAlertDescription.DecodeError));
    }

    [TestMethod]
    public void Decoders_SameBufferDecodedInParallel_AgreeAndLeaveTheBufferUnchanged()
    {
        byte[] data = ApplicationLayerProtocolNegotiationExtension.Encode(["h2", "http/1.1"]).Data;
        byte[] original = [.. data];
        string[][] answers = new string[256][];

        Parallel.For(0, answers.Length, index => answers[index] = [.. ApplicationLayerProtocolNegotiationExtension.Decode(data).Value]);
        Diagnostics.Act("answers", answers.Length);

        CollectionAssert.AreEqual(original, data);
        foreach (string[] answer in answers)
        {
            CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, answer);
        }
    }

    [TestMethod]
    public void HandshakeReader_SameBufferReadRepeatedly_AnswersTheSameEveryTime()
    {
        byte[] buffer = new HandshakeMessage(HandshakeType.NewSessionTicket, [9, 8, 7]).Encode();

        HandshakeMessageReadResult first = HandshakeMessageReader.Read(buffer);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            HandshakeMessageReadResult again = HandshakeMessageReader.Read(buffer);
            Assert.AreEqual(first.BytesConsumed, again.BytesConsumed);
            CollectionAssert.AreEqual(first.Message!.Body, again.Message!.Body);
        }

        Diagnostics.Act("consumed", first.BytesConsumed);
    }
}
