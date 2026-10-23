using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// A refused dial of an IPv6 zone-id host: curl 8.22.0 names the host without its zone,
/// <c>http://[::1%25lo]:9/</c> -> <c>curl: (7) Failed to connect to ::1:9 after 9 ms: Could not connect to
/// server</c> (measured 2026-10-22, BL-3182).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_ToARefusedZoneIdHost_FailsWithExit7NamingTheHostWithoutItsZone()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.IPv6Loopback), dialer, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("::1%lo", 9, UseTls: false));

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        StringAssert.Matches(result.ErrorMessage!, new System.Text.RegularExpressions.Regex(@"^Failed to connect to ::1:9 after \d+ ms: Could not connect to server$"));
    }
}
