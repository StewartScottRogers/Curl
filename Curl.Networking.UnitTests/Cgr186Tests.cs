using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-186 of the third-party gap report (BL-3715): off Windows, <c>-6</c> with an IPv4 literal
/// URL has nothing to dial, as curl 8.18.0 on Linux fails with exit 7; Windows still connects.
/// </summary>
[TestClass]
public sealed class Cgr186Tests
{
    private static readonly IReadOnlyList<IPAddress> Loopback = [IPAddress.Loopback];

    [TestMethod]
    public void Dialable_Ipv4LiteralUnderSixOffWindows_IsEmpty() =>
        Assert.AreEqual(0, AddressFamilyFilter.Dialable("127.0.0.1", Loopback, AddressFamily.InterNetworkV6, isWindows: false).Count);

    [TestMethod]
    public void Dialable_Ipv4LiteralUnderSixOnWindows_IsDialledAsWritten() =>
        Assert.AreEqual(1, AddressFamilyFilter.Dialable("127.0.0.1", Loopback, AddressFamily.InterNetworkV6, isWindows: true).Count);

    [TestMethod]
    public void Dialable_Ipv4LiteralUnderFourOffWindows_IsKept() =>
        Assert.AreEqual(1, AddressFamilyFilter.Dialable("127.0.0.1", Loopback, AddressFamily.InterNetwork, isWindows: false).Count);

    [TestMethod]
    public void IsRefusedLiteral_ChecksFamilyPlatformAndLiteral()
    {
        Assert.IsTrue(AddressFamilyFilter.IsRefusedLiteral("127.0.0.1", AddressFamily.InterNetworkV6, isWindows: false));
        Assert.IsFalse(AddressFamilyFilter.IsRefusedLiteral("127.0.0.1", AddressFamily.InterNetworkV6, isWindows: true));
        Assert.IsFalse(AddressFamilyFilter.IsRefusedLiteral("127.0.0.1", AddressFamily.Unspecified, isWindows: false));
        Assert.IsFalse(AddressFamilyFilter.IsRefusedLiteral("example.test", AddressFamily.InterNetworkV6, isWindows: false));
    }

    [TestMethod]
    public async Task ConnectAsync_Ipv4LiteralUnderSixWhereLiteralsAreFiltered_FailsWithExit7()
    {
        var connector = Connector(filtersAddressLiterals: true);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 20001, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:20001 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_Ipv4LiteralUnderSixWhereLiteralsAreDialledAsWritten_Connects()
    {
        var connector = Connector(filtersAddressLiterals: false);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 20001, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    private static TcpConnector Connector(bool filtersAddressLiterals) =>
        new(
            new FakeDnsResolver(IPAddress.Loopback),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            addressFamily: AddressFamily.InterNetworkV6)
        {
            FiltersAddressLiterals = filtersAddressLiterals,
        };}
