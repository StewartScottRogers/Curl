using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

[TestClass]
public sealed class RetransmissionTimeoutEstimatorTests
{
    [TestMethod]
    public void Timeout_NoSample_IsOneSecond()
    {
        RetransmissionTimeoutEstimator estimator = new();

        Assert.AreEqual(TimeSpan.FromSeconds(1), estimator.Timeout);
        Assert.IsNull(estimator.SmoothedRoundTripTime);
    }

    [TestMethod]
    public void AddSample_FirstSample_IsSmoothedPlusFourHalves()
    {
        RetransmissionTimeoutEstimator estimator = new();

        estimator.AddSample(TimeSpan.FromSeconds(10));

        Assert.AreEqual(TimeSpan.FromSeconds(10), estimator.SmoothedRoundTripTime);
        Assert.AreEqual(TimeSpan.FromSeconds(30), estimator.Timeout);
    }

    [TestMethod]
    public void AddSample_SecondSample_SmoothsWithAnEighthAndAQuarter()
    {
        RetransmissionTimeoutEstimator estimator = new();
        estimator.AddSample(TimeSpan.FromSeconds(10));

        estimator.AddSample(TimeSpan.FromSeconds(6));

        Assert.AreEqual(TimeSpan.FromSeconds(9.5), estimator.SmoothedRoundTripTime);
        Assert.AreEqual(TimeSpan.FromSeconds(28.5), estimator.Timeout);
    }

    [TestMethod]
    [DataRow(0.0, 1.0)]
    [DataRow(0.1, 1.0)]
    [DataRow(40.0, 60.0)]
    public void AddSample_OutsideTheBounds_ClampsToOneAndSixtySeconds(double seconds, double expected)
    {
        RetransmissionTimeoutEstimator estimator = new();

        estimator.AddSample(TimeSpan.FromSeconds(seconds));

        Assert.AreEqual(TimeSpan.FromSeconds(expected), estimator.Timeout);
    }

    [TestMethod]
    public void BackOff_Repeated_DoublesUpToSixtySeconds()
    {
        RetransmissionTimeoutEstimator estimator = new();
        List<double> timeouts = [];

        for (var index = 0; index < 7; index++)
        {
            estimator.BackOff();
            timeouts.Add(estimator.Timeout.TotalSeconds);
        }

        CollectionAssert.AreEqual(new List<double> { 2, 4, 8, 16, 32, 60, 60 }, timeouts);
    }
}
