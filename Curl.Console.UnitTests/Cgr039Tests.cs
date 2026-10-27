using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Pins CGR-039 (BL-3568) in <see cref="EasyOptionsFromCommandLine" />: the very large
/// <c>--connect-timeout</c>, <c>--keepalive-time</c> and <c>--speed-time</c> values curl's 64-bit
/// <c>long</c> accepts on Linux and macOS map to the longest wait the engine can arm instead of
/// failing with a .NET exception, and smaller values map unchanged. Windows curl's 32-bit
/// <c>long</c> refuses those values while parsing, so the command-line tests run off Windows.
/// </summary>
[TestClass]
public sealed class Cgr039Tests
{
    private static readonly TimeSpan LongestTimerWait = TimeSpan.FromMilliseconds(4294967294);

    private static readonly TimeSpan MostTimeSpanSeconds = TimeSpan.FromSeconds(922337203685);

    [TestMethod]
    public void AtMostLongestTimerWait_WaitPastTheLongestTimerWait_IsTheLongestTimerWait()
    {
        TimeSpan wait = EasyOptionsFromCommandLine.AtMostLongestTimerWait(TimeSpan.FromSeconds(4294968));

        Assert.AreEqual(LongestTimerWait, wait);
    }

    [TestMethod]
    public void AtMostLongestTimerWait_WaitWithinTheLongestTimerWait_IsKept()
    {
        TimeSpan wait = EasyOptionsFromCommandLine.AtMostLongestTimerWait(TimeSpan.FromSeconds(4294967));

        Assert.AreEqual(TimeSpan.FromSeconds(4294967), wait);
    }

    [TestMethod]
    public void AtMostTimeSpanSeconds_SecondsPastATimeSpan_IsTheMostWholeSecondsATimeSpanHolds()
    {
        long seconds = EasyOptionsFromCommandLine.AtMostTimeSpanSeconds(922337203686);

        Assert.AreEqual(922337203685, seconds);
    }

    [TestMethod]
    public void AtMostTimeSpanSeconds_SecondsWithinATimeSpan_IsKept()
    {
        long seconds = EasyOptionsFromCommandLine.AtMostTimeSpanSeconds(4294968);

        Assert.AreEqual(4294968, seconds);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void NetworkOf_ConnectTimeoutPastTheLongestTimerWait_IsTheLongestTimerWait()
    {
        CommandLineOptions options = Parse("--connect-timeout", "4294968");

        Assert.AreEqual(LongestTimerWait, EasyOptionsFromCommandLine.NetworkOf(options).ConnectTimeout);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TransferOf_MaxTimePastTheLongestTimerWait_IsTheLongestTimerWait()
    {
        CommandLineOptions options = Parse("-m", "4294968");

        Assert.AreEqual(LongestTimerWait, EasyOptionsFromCommandLine.TransferOf(options).Timeout);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void NetworkOf_KeepAliveTimePastATimeSpan_IsTheMostWholeSecondsATimeSpanHolds()
    {
        CommandLineOptions options = Parse("--keepalive-time", "922337203686");

        var network = EasyOptionsFromCommandLine.NetworkOf(options);

        Assert.AreEqual(MostTimeSpanSeconds, network.TcpKeepIdle);
        Assert.AreEqual(MostTimeSpanSeconds, network.TcpKeepInterval);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void TransferOf_SpeedTimePastATimeSpan_IsTheMostWholeSecondsATimeSpanHolds()
    {
        CommandLineOptions options = Parse("--speed-time", "922337203686");

        Assert.AreEqual(MostTimeSpanSeconds, EasyOptionsFromCommandLine.TransferOf(options).LowSpeedTime);
    }

    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult result = QuicBuildParser.Parse([.. arguments, "http://127.0.0.1/"], _ => true);
        Assert.IsTrue(result.IsAccepted, string.Join(' ', arguments));
        return result.Options;
    }
}
