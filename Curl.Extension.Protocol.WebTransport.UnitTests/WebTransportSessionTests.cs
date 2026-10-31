using Curl.Extension.Protocol.WebTransport.Wire;
using Curl.Http3;

namespace Curl.Extension.Protocol.WebTransport.Session;

/// <summary>Checks which server streams and statuses a session takes (draft-ietf-webtrans-http3-16).</summary>
[TestClass]
public sealed class WebTransportSessionTests
{
    [TestMethod]
    [DataRow((int)WebTransportMode.Unidirectional, false, WebTransportWire.UnidirectionalStreamType, true)]
    [DataRow((int)WebTransportMode.Bidirectional, false, WebTransportWire.UnidirectionalStreamType, false)]
    [DataRow((int)WebTransportMode.Unidirectional, true, WebTransportWire.UnidirectionalStreamType, false)]
    [DataRow((int)WebTransportMode.Unidirectional, false, 0x21L, false)]
    public void IsSessionUnidirectionalStream_Section4_2_TakesOnlyType0x54UnidirectionalStreamsInAUniTransfer(int mode, bool isBidirectional, long type, bool expected)
    {
        Http3UnknownPeerStream peer = new(new InMemoryMultiplexedStream(3), isBidirectional, type);

        bool taken = WebTransportSession.IsSessionUnidirectionalStream(peer, (WebTransportMode)mode);

        Assert.AreEqual(expected, taken);
    }

    [TestMethod]
    [DataRow("200", true)]
    [DataRow("204", true)]
    [DataRow("404", false)]
    [DataRow("2", false)]
    [DataRow(null, false)]
    public void IsSuccessStatus_Section3_3_AcceptsOnly2xx(string? status, bool expected) =>
        Assert.AreEqual(expected, WebTransportSession.IsSuccessStatus(status));
}
