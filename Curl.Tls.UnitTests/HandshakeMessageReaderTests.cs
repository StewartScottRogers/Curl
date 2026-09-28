using static Curl.Tls.Rfc8448Messages;

namespace Curl.Tls;

/// <summary>
/// Checks <see cref="HandshakeMessageReader" /> frames RFC 8448's section 3 server flight
/// one message at a time, waits for a header or body that is not all there, and rejects
/// an unknown message type with <c>unexpected_message</c>.
/// </summary>
[TestClass]
public sealed class HandshakeMessageReaderTests
{
    // RFC 8448 section 3: the server's encrypted flight is four messages back to back.
    [TestMethod]
    public void ReadFramesEachMessageOfTheSimpleHandshakeServerFlight()
    {
        byte[] flight = Convert.FromHexString(SimpleEncryptedExtensions + SimpleCertificate + SimpleCertificateVerify + SimpleServerFinished);
        List<HandshakeType> types = [];
        int offset = 0;
        while (offset < flight.Length)
        {
            HandshakeMessageReadResult read = HandshakeMessageReader.Read(flight.AsSpan(offset));
            Assert.AreEqual(HandshakeMessageReadStatus.Complete, read.Status);
            Assert.IsNull(read.Alert);
            types.Add(read.Message!.Type);
            CollectionAssert.AreEqual(flight[offset..(offset + read.BytesConsumed)], read.Message.Encode());
            offset += read.BytesConsumed;
        }

        CollectionAssert.AreEqual(
            new[] { HandshakeType.EncryptedExtensions, HandshakeType.Certificate, HandshakeType.CertificateVerify, HandshakeType.Finished },
            types);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("140000")]
    [DataRow("14000020")]
    [DataRow("140000209b9b141d906337fbd2cbdce71df4deda4ab42c309572cb7fffee5454b78f07")]
    public void ReadWaitsForAHeaderOrBodyThatIsNotAllThere(string partial)
    {
        HandshakeMessageReadResult read = HandshakeMessageReader.Read(Convert.FromHexString(partial));

        Assert.AreEqual(HandshakeMessageReadStatus.NeedMoreBytes, read.Status);
        Assert.IsNull(read.Message);
        Assert.AreEqual(0, read.BytesConsumed);
        Assert.IsNull(read.Alert);
    }

    [TestMethod]
    [DataRow("03000000")]
    [DataRow("63000000")]
    [DataRow("ff000000")]
    public void ReadRejectsAnUnknownMessageTypeWithUnexpectedMessage(string header)
    {
        HandshakeMessageReadResult read = HandshakeMessageReader.Read(Convert.FromHexString(header));

        Assert.AreEqual(HandshakeMessageReadStatus.Failed, read.Status);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, read.Alert);
        Assert.IsNull(read.Message);
    }

    [TestMethod]
    public void ReadTakesOnlyTheFirstMessageWhenMoreFollow()
    {
        HandshakeMessageReadResult read = HandshakeMessageReader.Read(Convert.FromHexString("0500000014"));

        Assert.AreEqual(HandshakeType.EndOfEarlyData, read.Message!.Type);
        Assert.IsEmpty(read.Message.Body);
        Assert.AreEqual(4, read.BytesConsumed);
    }
}
