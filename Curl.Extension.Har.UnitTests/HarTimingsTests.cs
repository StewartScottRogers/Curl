namespace Curl.Extension.Har.UnitTests;

[TestClass]
public sealed class HarTimingsTests
{
    private const long Frequency = 1000;

    [TestMethod]
    public void Of_EveryPointRecorded_GivesEachPhaseInMillisecondsWithSslInsideConnect_Har12Timings()
    {
        TransferTimings timings = new(0, new ConnectTimings(5, 15, 35, 50), 52, 60, 100, 130);

        var phases = HarTimings.Of(timings, Frequency);

        Assert.AreEqual(new HarTimings(5, 10, 35, 15, 8, 40, 30), phases);
        Assert.AreEqual(128, phases.Total);
    }

    [TestMethod]
    public void Of_ATimestampFrequencyOtherThanMilliseconds_ConvertsToMilliseconds()
    {
        TransferTimings timings = new(0, null, null, null, 1, 3);

        Assert.AreEqual(2000, HarTimings.Of(timings, 1).Receive);
    }

    [TestMethod]
    public void Of_NoTimings_GivesMinusOneForTheOptionalPhasesAndZeroForTheRequiredOnes_Har12Timings()
    {
        var phases = HarTimings.Of(null, Frequency);

        Assert.AreEqual(new HarTimings(-1, -1, -1, -1, 0, 0, 0), phases);
        Assert.AreEqual(0, phases.Total);
    }

    [TestMethod]
    public void Of_AReusedConnection_HasNoConnectPhasesAndSendsFromTheStart()
    {
        TransferTimings timings = new(10, null, null, 14, 20, 25);

        Assert.AreEqual(new HarTimings(-1, -1, -1, -1, 4, 6, 5), HarTimings.Of(timings, Frequency));
    }

    [TestMethod]
    public void Of_AConnectThatNeverResolvedOrConnected_GivesMinusOneForDnsConnectAndSsl()
    {
        TransferTimings timings = new(0, new ConnectTimings(2, null, null, null), null, null, null, 9);

        Assert.AreEqual(new HarTimings(2, -1, -1, -1, 0, 0, 0), HarTimings.Of(timings, Frequency));
    }

    [TestMethod]
    public void Of_NoRequestReadyAfterATlsHandshake_SendsFromTheHandshakesEnd()
    {
        TransferTimings timings = new(0, new ConnectTimings(0, 1, 3, 6), null, 10, null, 12);

        Assert.AreEqual(4, HarTimings.Of(timings, Frequency).Send);
    }

    [TestMethod]
    public void Of_APlainConnection_ConnectsFromTheResolveAndHasNoSsl()
    {
        TransferTimings timings = new(0, new ConnectTimings(0, null, 7, null), null, 9, null, 12);

        Assert.AreEqual(new HarTimings(0, -1, 7, -1, 2, 0, 0), HarTimings.Of(timings, Frequency));
    }
}
