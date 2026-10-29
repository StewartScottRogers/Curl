using Curl.Api.Options;
using Curl.Api.Options.Protocols;
using Libcurl.Options;
using static Libcurl.UnitTests.Options.Protocols.ProtocolOptionAssert;

namespace Libcurl.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasyRtspWebSocketOptionSettersTests
{
    [TestMethod]
    public void SetOption_RtspRequest_TakesEachRequestAndRefusesAnUnknownOne()
    {
        Assert.AreEqual(RtspRequest.Receive, SetOk(CurlOption.RtspRequest, 11L).Rtsp.Request);
        Assert.AreEqual(RtspRequest.Setup, SetOk(CurlOption.RtspRequest, RtspRequest.Setup).Rtsp.Request);
        var options = new EasyOptions();
        Assert.AreEqual(Curl.Api.Codes.CurlCode.BadFunctionArgument, EasyOptionDispatch.Default.SetOption(ref options, CurlOption.RtspRequest, -1L));
    }

    [TestMethod]
    public void SetOption_RtspSessionId_SetsTheSessionAndNullClearsIt()
    {
        Text(CurlOption.RtspSessionId, p => p.Rtsp.SessionId);
    }

    [TestMethod]
    public void SetOption_RtspStreamUri_SetsTheUriAndNullRestoresTheStar()
    {
        var options = SetOk(new EasyOptions(), CurlOption.RtspStreamUri, "rtsp://a/s");

        Assert.AreEqual("rtsp://a/s", options.Protocols.Rtsp.StreamUri);
        Assert.AreEqual("*", SetOk(options, CurlOption.RtspStreamUri, null).Protocols.Rtsp.StreamUri);
    }

    [TestMethod]
    public void SetOption_RtspSessionIdNullTwice_CountsEachSet()
    {
        var options = SetOk(SetOk(new EasyOptions(), CurlOption.RtspSessionId, null), CurlOption.RtspSessionId, null);

        Assert.IsNull(options.Protocols.Rtsp.SessionId);
        Assert.AreEqual(2L, options.Protocols.Rtsp.SessionIdSetCount);
    }

    [TestMethod]
    public void SetOption_RtspTransport_SetsTheTransportAndNullClearsIt()
    {
        Text(CurlOption.RtspTransport, p => p.Rtsp.Transport);
    }

    [TestMethod]
    public void SetOption_RtspClientCseq_SetsTheNextClientCSeq()
    {
        Assert.AreEqual(42L, SetOk(CurlOption.RtspClientCseq, 42L).Rtsp.ClientCSeq);
    }

    [TestMethod]
    public void SetOption_RtspServerCseq_SetsTheExpectedServerCSeq()
    {
        Assert.AreEqual(7L, SetOk(CurlOption.RtspServerCseq, 7).Rtsp.ServerCSeq);
    }

    [TestMethod]
    public void SetOption_WsOptions_KeepsOnlyTheRawModeAndNoAutoPongBits()
    {
        var both = WebSocketOptionBits.RawMode | WebSocketOptionBits.NoAutoPong;

        Assert.AreEqual(both, SetOk(CurlOption.WsOptions, 0xFFL).WebSocket.Options);
        Assert.AreEqual(WebSocketOptionBits.NoAutoPong, SetOk(CurlOption.WsOptions, 2L).WebSocket.Options);
        Assert.AreEqual(WebSocketOptionBits.None, SetOk(CurlOption.WsOptions, 4L).WebSocket.Options);
    }
}
