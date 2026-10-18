namespace Curl.Engine;

[TestClass]
public sealed class FirstTransferFtpPwdStopTests
{
    [TestMethod]
    public void TryClaim_CalledTwice_ClaimsOnlyTheFirstTime()
    {
        FirstTransferFtpPwdStop stop = new();

        bool first = stop.TryClaim();
        bool second = stop.TryClaim();

        Assert.IsTrue(first);
        Assert.IsFalse(second);
    }

    [TestMethod]
    public void When_NotAsked_GivesNone()
    {
        FirstTransferFtpPwdStop? stop = FirstTransferFtpPwdStop.When(asked: false);

        Assert.IsNull(stop);
    }

    [TestMethod]
    public void When_Asked_GivesAnUnclaimedStop()
    {
        FirstTransferFtpPwdStop? stop = FirstTransferFtpPwdStop.When(asked: true);

        Assert.IsNotNull(stop);
        Assert.IsTrue(stop.TryClaim());
    }
}
