using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> with <c>--connect-to</c> mappings parsed under a feature mask
/// that hides IPv6: an IPv6 destination fails the connect with exit 4 and curl's message, as
/// upstream test1454 has it, while a malformed destination without the mask stays exit 49.
/// </summary>
[TestClass]
public sealed class TcpConnectorConnectToNotBuiltInTests
{
    [TestMethod]
    public async Task ConnectAsync_MaskHidingIpv6WithBracketedDestination_FailsWithExit4AndDialsNothing()
    {
        // curl --connect-to localhost:80:[::1]:80 http://localhost/ in a build without IPv6 (test1454)
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["localhost:80:[::1]:80"], hidesIpv6: true));

        var result = await connector.ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.NotBuiltIn, result.ExitCode);
        Assert.AreEqual("Use of IPv6 in *_CONNECT_TO without IPv6 support built-in", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_NoMaskWithMalformedDestination_FailsWithExit49()
    {
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.Loopback), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["localhost:80:b:x"], hidesIpv6: false));

        var result = await connector.ConnectAsync(new ConnectTarget("localhost", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
        Assert.AreEqual("No valid port number in 'b:x'", result.ErrorMessage);
    }
}
