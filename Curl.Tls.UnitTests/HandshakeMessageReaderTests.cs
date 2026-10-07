using Curl.Testing;
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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // RFC 8448 section 3: the server's encrypted flight is four messages back to back.
    [TestMethod]
    public void ReadFramesEachMessageOfTheSimpleHandshakeServerFlight()
    {
        byte[] flight = Convert.FromHexString(SimpleEncryptedExtensions + SimpleCertificate + SimpleCertificateVerify + SimpleServerFinished);
        Diagnostics.Bytes("server flight", flight);
        Diagnostics.Arrange("server flight length", flight.Length);
        List<HandshakeType> types = [];
        int offset = 0;
        while (offset < flight.Length)
        {
            HandshakeMessageReadResult read = HandshakeMessageReader.Read(flight.AsSpan(offset));
            WriteRead($"at offset {offset}", read);
            Assert.AreEqual(HandshakeMessageReadStatus.Complete, read.Status);
            Assert.IsNull(read.Alert);
            types.Add(read.Message!.Type);
            Diagnostics.Diff($"re-encoded {read.Message.Type}", flight[offset..(offset + read.BytesConsumed)], read.Message.Encode());
            CollectionAssert.AreEqual(flight[offset..(offset + read.BytesConsumed)], read.Message.Encode());
            offset += read.BytesConsumed;
        }

        Diagnostics.Assert("message types", "EncryptedExtensions, Certificate, CertificateVerify, Finished", string.Join(", ", types));
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
        Diagnostics.Arrange("partial message", partial);

        HandshakeMessageReadResult read = HandshakeMessageReader.Read(Convert.FromHexString(partial));

        WriteRead("partial message", read);
        Diagnostics.Assert("status", HandshakeMessageReadStatus.NeedMoreBytes, read.Status);
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
        Diagnostics.Arrange("header", header);

        HandshakeMessageReadResult read = HandshakeMessageReader.Read(Convert.FromHexString(header));

        WriteRead("header", read);
        Diagnostics.Assert("alert", TlsAlertDescription.UnexpectedMessage, read.Alert);
        Assert.AreEqual(HandshakeMessageReadStatus.Failed, read.Status);
        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, read.Alert);
        Assert.IsNull(read.Message);
    }

    [TestMethod]
    public void ReadTakesOnlyTheFirstMessageWhenMoreFollow()
    {
        Diagnostics.Arrange("bytes", "0500000014");

        HandshakeMessageReadResult read = HandshakeMessageReader.Read(Convert.FromHexString("0500000014"));

        WriteRead("bytes", read);
        Diagnostics.Assert("bytes consumed", 4, read.BytesConsumed);
        Assert.AreEqual(HandshakeType.EndOfEarlyData, read.Message!.Type);
        Assert.IsEmpty(read.Message.Body);
        Assert.AreEqual(4, read.BytesConsumed);
    }

    /// <summary>Writes a read's status, message type, bytes consumed and alert as one ACT line.</summary>
    private void WriteRead(string what, HandshakeMessageReadResult read) =>
        Diagnostics.Act(
            $"read {what}",
            $"status {read.Status}, message {read.Message?.Type.ToString() ?? "none"}, consumed {read.BytesConsumed}, alert {(read.Alert is { } alert ? $"{alert} ({(byte)alert})" : "none")}");
}
