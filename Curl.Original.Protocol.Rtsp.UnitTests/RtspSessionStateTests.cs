namespace Curl.Original.Protocol.Rtsp;

[TestClass]
public sealed class RtspSessionStateTests
{
    [TestMethod]
    public void ForgetSessionId_AfterAReplyGaveOne_HoldsNoneAndTakesTheNextReplysId()
    {
        RtspSessionState state = new();
        state.AcceptSession("first ");

        state.ForgetSessionId();

        Assert.IsNull(state.SessionId);
        state.AcceptSession("second ");
        Assert.AreEqual("second", state.SessionId);
    }

    [TestMethod]
    [DataRow(" RTP/AVP/TCP;unicast;interleaved=0-1\r\n", "0,1")]
    [DataRow("RTP/AVP/TCP; INTERLEAVED=5", "5")]
    [DataRow("RTP/AVP/TCP;interleaved=3-x", "3")]
    [DataRow("RTP/AVP/TCP;interleaved=x;interleaved=4", "")]
    [DataRow("RTP/AVP/TCP;interleaved=256", "")]
    [DataRow("RTP/AVP/TCP;interleaved=7-6", "")]
    [DataRow("RTP/AVP/TCP;unicast", "")]
    public void AcceptTransport_InterleavedParameter_SetsUpItsChannels(string transport, string expected)
    {
        RtspSessionState state = new();

        state.AcceptTransport(transport);

        string setUp = string.Join(",", Enumerable.Range(0, 256).Where(channel => state.IsChannelSetUp((byte)channel)));
        Assert.AreEqual(expected, setUp);
    }

    [TestMethod]
    public void AcceptTransport_TwoReplies_AddTheirChannelsTogether()
    {
        RtspSessionState state = new();

        state.AcceptTransport("RTP/AVP/TCP;interleaved=0");
        state.AcceptTransport("RTP/AVP/TCP;interleaved=255");

        Assert.IsTrue(state.IsChannelSetUp(0));
        Assert.IsTrue(state.IsChannelSetUp(255));
        Assert.IsFalse(state.IsChannelSetUp(1));
    }

    [TestMethod]
    public void StartTransfer_NewConnection_ForgetsTheCarriedBytesAndAReusedOneKeepsThem()
    {
        RtspSessionState state = new() { CarriedBytes = [1] };

        state.StartTransfer(null, connectionReused: true);
        Assert.HasCount(1, state.CarriedBytes);

        state.StartTransfer(null, connectionReused: false);
        Assert.IsEmpty(state.CarriedBytes);
    }
}
