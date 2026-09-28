namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default members of <see cref="ITransferEvents" /> and the TLS event records they
/// take (ADR-0085).
/// </summary>
[TestClass]
public sealed class ITransferEventsTests
{
    [TestMethod]
    public void ReportTlsMessage_NotOverridden_ReportsTheBytesAsTlsData()
    {
        var sink = new TlsDataRecordingEvents();
        TlsMessageEvent message = new()
        {
            ProtocolVersion = 0x0304,
            ContentType = TlsContentType.Handshake,
            Sent = true,
            Bytes = new byte[] { 1, 0, 0 },
        };

        ((ITransferEvents)sink).ReportTlsMessage(message);

        CollectionAssert.AreEqual(new byte[] { 1, 0, 0 }, sink.Bytes);
        Assert.IsTrue(sink.Sent);
        Assert.AreEqual(0x0304, message.ProtocolVersion);
        Assert.AreEqual(TlsContentType.Handshake, message.ContentType);
    }

    [TestMethod]
    public void ReportTlsTrust_NotOverridden_DoesNothing()
    {
        var sink = new TlsDataRecordingEvents();
        TlsTrustEvent trust = new()
        {
            VerifiesPeer = true,
            HasCaCertificateBlob = true,
            CaCertificateFile = "/f.pem",
            CaCertificateDirectory = "/d",
        };

        ((ITransferEvents)sink).ReportTlsTrust(trust);

        Assert.IsNull(sink.Bytes);
        Assert.IsTrue(trust.VerifiesPeer && trust.HasCaCertificateBlob);
        Assert.AreEqual("/f.pem", trust.CaCertificateFile);
        Assert.AreEqual("/d", trust.CaCertificateDirectory);
    }

    [TestMethod]
    public void TlsTrustEvent_OnlyVerificationGiven_HasNoTrustAnchorSource()
    {
        TlsTrustEvent trust = new() { VerifiesPeer = false };

        Assert.IsFalse(trust.HasCaCertificateBlob);
        Assert.IsNull(trust.CaCertificateFile);
        Assert.IsNull(trust.CaCertificateDirectory);
    }

    private sealed class TlsDataRecordingEvents : ITransferEvents
    {
        public byte[]? Bytes { get; private set; }

        public bool Sent { get; private set; }

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)
        {
            Bytes = bytes.ToArray();
            Sent = sent;
        }

        public void ReportInfo(string text) => throw new NotSupportedException();

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => throw new NotSupportedException();

        public void ReportConnectionReused(ConnectionReusedEvent reused) => throw new NotSupportedException();

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => throw new NotSupportedException();

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => throw new NotSupportedException();
    }
}
