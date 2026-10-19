namespace Curl.Protocol.Rtsp;

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
}
