using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives a socket the operating system will not open, as one without Multipath TCP refuses
/// <c>--mptcp</c>'s: curl reports <c>failed to open socket</c> in place of <c>Trying</c>, moves on, and
/// ends with exit 7 when no socket opened (measured 2026-10-01, BL-647 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly SocketException ProtocolNotSupported = new((int)SocketError.ProtocolNotSupported);

    [TestMethod]
    public async Task ConnectAsync_WhenNoSocketOpens_ReportsTheOpenFailureAndFailsWithExit7()
    {
        // curl -v --mptcp http://127.0.0.1:41647/ (Windows, 8.21.0) ->
        // * failed to open socket: The system could not find the environment option that was entered.
        // * connect to  port 0 from  port 0 failed: No error
        // * Failed to connect to 127.0.0.1:41647 after 0 ms: Could not connect to server
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { SocketOpenOutcome = _ => ProtocolNotSupported };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 41647, UseTls: false) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        const string failure = "Failed to connect to 127.0.0.1:41647 after 0 ms: Could not connect to server";
        CollectionAssert.AreEqual(
            AddressFamilyRace.SocketOpenFailedLines(ProtocolNotSupported, OperatingSystem.IsWindows()).Append(failure).ToArray(),
            events.Info);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(failure, result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenOnlyTheFirstFamilysSocketFails_DialsTheOtherFamilyAtOnce()
    {
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ => new FakeConnection(),
            SocketOpenOutcome = family => family == AddressFamily.InterNetworkV6 ? ProtocolNotSupported : null,
        };
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.IPv6Loopback, Loopback), dialer, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 80) }, dialer.DialedEndPoints);
        CollectionAssert.Contains(events.Info, "  Trying 127.0.0.1:80...");
        CollectionAssert.DoesNotContain(events.Info, "  Trying [::1]:80...");
    }

    [TestMethod]
    public void SocketOpenFailedLines_OnWindows_AreTheSchannelBuildsTwoLines()
    {
        Diagnostics.Arrange("socket error", "ProtocolNotSupported, on Windows");

        var lines = AddressFamilyRace.SocketOpenFailedLines(ProtocolNotSupported, onWindows: true).ToArray();

        Diagnostics.Act("line count", lines.Length);
        Diagnostics.Assert("line count", 2, lines.Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "failed to open socket: The system could not find the environment option that was entered.",
                "connect to  port 0 from  port 0 failed: No error",
            },
            lines);
    }

    [TestMethod]
    public void SocketOpenFailedLines_Elsewhere_IsTheOneLineWithTheSystemsReason()
    {
        Diagnostics.Arrange("socket error", "ProtocolNotSupported, elsewhere");

        var lines = AddressFamilyRace.SocketOpenFailedLines(ProtocolNotSupported, onWindows: false).ToArray();

        Diagnostics.Act("line count", lines.Length);
        Diagnostics.Assert("line count", 1, lines.Length);
        CollectionAssert.AreEqual(
            new[] { $"failed to open socket: {ProtocolNotSupported.Message}" },
            lines);
    }
}
