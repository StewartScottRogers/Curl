namespace Curl.Extended.Protocol.Coap.Dtls.Wire;

[TestClass]
public sealed class DtlsReplayWindowTests
{
    [TestMethod]
    public void IsReplay_Rfc6347Section4_1_2_6_NothingIsAReplayBeforeTheFirstRecord()
    {
        Assert.IsFalse(new DtlsReplayWindow().IsReplay(0));
    }


    [TestMethod]
    public void IsReplay_Rfc6347Section4_1_2_6_ADuplicateIsAReplay()
    {
        DtlsReplayWindow window = new();
        window.MarkReceived(10);
        window.MarkReceived(7);

        Assert.IsTrue(window.IsReplay(10));
        Assert.IsTrue(window.IsReplay(7));
    }


    [TestMethod]
    public void IsReplay_Rfc6347Section4_1_2_6_AnUnseenNumberInTheWindowIsNot()
    {
        DtlsReplayWindow window = new();
        window.MarkReceived(100);

        Assert.IsFalse(window.IsReplay(99));
        Assert.IsFalse(window.IsReplay(100 - DtlsReplayWindow.Size + 1));
    }


    [TestMethod]
    public void IsReplay_Rfc6347Section4_1_2_6_ANumberLeftOfTheWindowIsTooOld()
    {
        DtlsReplayWindow window = new();
        window.MarkReceived(100);

        Assert.IsTrue(window.IsReplay(100 - DtlsReplayWindow.Size));
    }


    [TestMethod]
    public void MarkReceived_Rfc6347Section4_1_2_6_AHigherNumberAdvancesTheWindowAndKeepsWhatWasSeen()
    {
        DtlsReplayWindow window = new();
        window.MarkReceived(5);
        window.MarkReceived(8);

        Assert.IsFalse(window.IsReplay(9));
        Assert.IsTrue(window.IsReplay(5));
        Assert.IsFalse(window.IsReplay(6));
        Assert.IsTrue(window.IsReplay(8));
    }


    [TestMethod]
    public void MarkReceived_Rfc6347Section4_1_2_6_AJumpPastTheWindowForgetsEverythingBefore()
    {
        DtlsReplayWindow window = new();
        window.MarkReceived(5);
        window.MarkReceived(5 + DtlsReplayWindow.Size);

        Assert.IsTrue(window.IsReplay(5));
        Assert.IsFalse(window.IsReplay(6));
        Assert.IsTrue(window.IsReplay(5 + DtlsReplayWindow.Size));
    }


    [TestMethod]
    public void MarkReceived_Rfc6347Section4_1_2_6_ANumberLeftOfTheWindowChangesNothing()
    {
        DtlsReplayWindow window = new();
        window.MarkReceived(200);
        window.MarkReceived(1);

        Assert.IsFalse(window.IsReplay(200 - DtlsReplayWindow.Size + 1));
        Assert.IsFalse(window.IsReplay(201));
    }
}
