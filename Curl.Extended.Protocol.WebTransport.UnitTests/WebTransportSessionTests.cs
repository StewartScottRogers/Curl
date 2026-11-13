using Curl.Extended.Protocol.WebTransport.Wire;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.WebTransport.Session;

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
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ConnectFields_TheOptions_FollowThePseudoHeadersInLowerCase()
    {
        ScriptedHttp3Connection connection = new() { AnswerStream = WebTransportProtocolHandlerTests.Echo };
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("webtransport://example.com/wt"),
            PostData = "x"u8.ToArray(),
            Output = new MemoryStream(),
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { UserAgent = "agW", CookieStrings = "c=1", Headers = ["Authorization: Bearer t", "Host: evil", ":path: /evil"] },
        };

        await new WebTransportProtocolHandler((_, _) => ValueTask.FromResult<IMultiplexedConnection>(connection)).ExecuteAsync(context);

        CollectionAssert.AreEqual(
            new WebTransportProtocolHandlerTests.HeaderFieldText[]
            {
                new(":method", "CONNECT"), new(":protocol", "webtransport-h3"), new(":scheme", "https"), new(":authority", "example.com"), new(":path", "/wt"),
                new("user-agent", "agW"), new("cookie", "c=1"), new("authorization", "Bearer t"),
            },
            WebTransportProtocolHandlerTests.DecodeRequest(connection.BidirectionalStreams[0].Written.ToArray()));
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
