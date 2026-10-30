using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// A refused loopback connect (<c>curl -sS http://127.0.0.1:9/</c>) fails on the first refused
/// dial, with no wait of Curl's own: off Windows curl 8.22.0 fails it at once (measured on Linux
/// and macOS, BL-3164 Notes), and on Windows the stack's own retry of a refused SYN is all the
/// delay there is, so curl 8.21.0 Schannel and Curl both take about two seconds there. The real
/// socket check times the fastest of several refusals, so a runner's scheduling stall does not
/// fail it.
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ConnectAsync_ToARefusedLoopbackPortOffWindows_FailsOnTheFirstDialWithNoWaitAndExit7()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var time = new ManualTimeProvider();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time);

        // The clock never moves, so any wait on the TimeProvider would leave this connect unfinished.
        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 9, UseTls: false)).WaitAsync(TimeSpan.FromSeconds(30));

        Diagnostics.Assert("dials", 1, dialer.DialedEndPoints.Count);
        Assert.AreEqual(1, dialer.DialedEndPoints.Count);
        Assert.AreEqual(0, time.PendingTimerCount);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("Failed to connect to 127.0.0.1:9 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ConnectAsync_ToARealRefusedLoopbackPortOffWindows_FailsWithExit7InUnder500Milliseconds()
    {
        using var unlistened = BoundUnlistenedSocket();
        var port = ((IPEndPoint)unlistened.LocalEndPoint!).Port;
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new TcpDialer(), new FakeTlsProvider(), TimeProvider.System);

        // One untimed connect first, so the timed ones measure the refusal, not the first use's
        // JIT and socket start-up on a busy runner (633 ms on macOS CI, BL-3845).
        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", port, UseTls: false));

        // The fastest of several refusals: a wait of Curl's own would slow every one of them,
        // while a busy runner's scheduler stalls only some (a 2 ms refusal then a 568 ms one on
        // macOS CI, BL-4006).
        var fastestMilliseconds = long.MaxValue;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", port, UseTls: false));
            stopwatch.Stop();

            Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
            Assert.IsTrue(result.IsConnectionRefused);
            fastestMilliseconds = Math.Min(fastestMilliseconds, stopwatch.ElapsedMilliseconds);
        }

        Diagnostics.Assert("fastest elapsed ms", "< 500", fastestMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.IsLessThan(500L, fastestMilliseconds);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_ToARefusedLoopbackPortOnWindows_DialsOnceAndLeavesTheSynRetryToTheStack()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var time = new ManualTimeProvider();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 9, UseTls: false)).WaitAsync(TimeSpan.FromSeconds(30));

        Diagnostics.Assert("dials", 1, dialer.DialedEndPoints.Count);
        Assert.AreEqual(1, dialer.DialedEndPoints.Count);
        Assert.AreEqual(0, time.PendingTimerCount);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("Failed to connect to 127.0.0.1:9 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    /// <summary>
    /// A loopback socket bound to a port the system handed it and never listening, so a connect to that port is
    /// refused; held for the whole test, so a test running beside it cannot be handed the port and accept the
    /// connect (BL-4368).
    /// </summary>
    private static Socket BoundUnlistenedSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }
}
