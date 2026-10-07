using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxies
/// with fakes. Every request byte and every message is curl 8.21.0's, measured against a
/// scripted loopback SOCKS server; the commands and bytes are in BL-213's Notes.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly byte[] Socks4Granted = [0x00, 0x5A, 0, 0, 0, 0, 0, 0];

    private static readonly byte[] Socks5NoAuthentication = [0x05, 0x00];

    private static readonly byte[] Socks5Succeeded = [0x05, 0x00, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0x1F, 0x90];

    private static readonly byte[] BytesAfterTheHandshake = [0xAA, 0xBB];

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks5)]
    public async Task ConnectAsync_ThroughSocks_ReportsNoMappedDestinationSoTheOriginIsNamedLeftIntact(ProxyKind kind)
    {
        // curl -v --socks5 127.0.0.1:18535 http://localhost:8080/ ->
        // * Connection #0 to host localhost:8080 left intact (the origin, not the proxy; BL-1074)
        byte[] reply = kind == ProxyKind.Socks4 ? Socks4Granted : [.. Socks5NoAuthentication, .. Socks5Succeeded];
        var (result, _) = await ConnectThroughSocksAsync(kind, "127.0.0.1", reply);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.MappedHost);
        Assert.AreEqual(0, result.MappedPort);
    }

    // SOCKS4

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4ToAnIPv4Literal_SendsTheAddressAndAnEmptyUserId()
    {
        // curl -x socks4://127.0.0.1:19080 http://127.0.0.1:8080/ sent 04 01 1f 90 7f 00 00 01 00
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "127.0.0.1", [.. Socks4Granted, .. BytesAfterTheHandshake]);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(proxyConnection, result.Connection);
        Assert.AreEqual(0, result.ProxyConnectResponseCode);
        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x04, 0x01, 0x1F, 0x90, 0x7F, 0x00, 0x00, 0x01, 0x00 }, [.. proxyConnection.Written]);
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x01, 0x1F, 0x90, 0x7F, 0x00, 0x00, 0x01, 0x00 }, proxyConnection.Written);
        Assert.AreEqual(BytesAfterTheHandshake.Length, proxyConnection.UnreadCount);
        Assert.IsFalse(proxyConnection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4ToAName_ResolvesItLocallyAndSendsTheUserNameButNotThePassword()
    {
        // curl -x socks4://bob:pw@127.0.0.1:19080 http://127.0.0.1:8080/ sent 04 01 1f 90 7f 00 00 01 62 6f 62 00
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks4,
            "target.example",
            Socks4Granted,
            new NetworkCredential("bob", "pw"),
            "target.example:8080:10.1.2.3");

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x04, 0x01, 0x1F, 0x90, 10, 1, 2, 3, 0x62, 0x6F, 0x62, 0x00 }, [.. proxyConnection.Written]);
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x01, 0x1F, 0x90, 10, 1, 2, 3, 0x62, 0x6F, 0x62, 0x00 }, proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4ToANameWithIPv6AndIPv4Addresses_SendsTheFirstIPv4Address()
    {
        var (_, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks4,
            "target.example",
            Socks4Granted,
            resolveEntry: "target.example:8080:[::1],10.1.2.3,10.9.9.9");

        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x04, 0x01, 0x1F, 0x90, 10, 1, 2, 3, 0x00 }, [.. proxyConnection.Written]);
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x01, 0x1F, 0x90, 10, 1, 2, 3, 0x00 }, proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4ToAnIPv6Literal_FailsWithProxyAndSendsNothing()
    {
        // curl -x socks4://127.0.0.1:19080 http://[::1]:8080/ -> curl: (97) SOCKS4 connection to ::1 not supported
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "::1", Socks4Granted);

        AssertProxyFailure(result, proxyConnection, "SOCKS4 connection to ::1 not supported");
        Assert.IsEmpty(proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4ToANameWithOnlyIPv6Addresses_FailsWithProxy()
    {
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks4,
            "target.example",
            Socks4Granted,
            resolveEntry: "target.example:8080:[2001:db8::1]");

        AssertProxyFailure(result, proxyConnection, "SOCKS4 connection to 2001:db8::1 not supported");
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks5)]
    public async Task ConnectAsync_ThroughALocallyResolvingSocksProxyToANameThatDoesNotResolve_FailsWithCouldntResolveHost(ProxyKind kind)
    {
        // curl -x socks4://127.0.0.1:19080 http://nosuch.invalid:8080/ -> curl: (6) Could not resolve host: nosuch.invalid
        var (result, proxyConnection) = await ConnectThroughSocksAsync(kind, "nosuch.invalid", Socks5NoAuthentication);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveHost, result.ExitCode);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nosuch.invalid", result.ErrorMessage);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks4a)]
    public async Task ConnectAsync_ThroughSocks4WithAUserNameOf256Bytes_FailsWithProxyAndSendsNothing(ProxyKind kind)
    {
        // curl -x socks4://<256 x a>@127.0.0.1:19080 http://127.0.0.1:8080/ -> curl: (97) Too long SOCKS proxy username
        var (result, proxyConnection) = await ConnectThroughSocksAsync(kind, "127.0.0.1", Socks4Granted, new NetworkCredential(new string('a', 256), string.Empty));

        AssertProxyFailure(result, proxyConnection, "Too long SOCKS proxy username");
        Assert.IsEmpty(proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4WithAUserNameOf255Bytes_SendsIt()
    {
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "127.0.0.1", Socks4Granted, new NetworkCredential(new string('a', 255), string.Empty));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(8 + 255 + 1, proxyConnection.Written);
    }

    [TestMethod]
    [DataRow((byte)0x5B, "request rejected or failed.")]
    [DataRow((byte)0x5C, "request rejected because SOCKS server cannot connect to identd on the client.")]
    [DataRow((byte)0x5D, "request rejected because the client program and identd report different user-ids.")]
    [DataRow((byte)0x5E, "Unknown.")]
    public async Task ConnectAsync_WhenTheSocks4ProxyRejects_FailsWithProxyNamingTheRepliedAddress(byte code, string reason)
    {
        // The proxy answered 00 5b 12 34 c0 a8 01 02 ->
        // curl: (97) [SOCKS] cannot complete SOCKS4 connection to 192.168.1.2:4660. (91), request rejected or failed.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "127.0.0.1", [0x00, code, 0x12, 0x34, 192, 168, 1, 2]);

        AssertProxyFailure(result, proxyConnection, $"[SOCKS] cannot complete SOCKS4 connection to 192.168.1.2:4660. ({code}), {reason}");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks4ReplyHasTheWrongVersion_FailsWithProxy()
    {
        // The proxy answered 01 5a 00 00 00 00 00 00 -> curl: (97) SOCKS4 reply has wrong version, version should be 0.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "127.0.0.1", [0x01, 0x5A, 0, 0, 0, 0, 0, 0]);

        AssertProxyFailure(result, proxyConnection, "SOCKS4 reply has wrong version, version should be 0.");
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks4a)]
    public async Task ConnectAsync_WhenTheSocks4ProxyClosesMidReply_FailsWithProxy(ProxyKind kind)
    {
        // The proxy answered 00 5a 00 and closed -> curl: (97) Failed to receive SOCKS response, proxy closed connection
        var (result, proxyConnection) = await ConnectThroughSocksAsync(kind, "127.0.0.1", [0x00, 0x5A, 0x00]);

        AssertProxyFailure(result, proxyConnection, "Failed to receive SOCKS response, proxy closed connection");
    }

    // SOCKS4a

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4aToAName_SendsTheNameForTheProxyToResolve()
    {
        // curl -x socks4a://127.0.0.1:19080 http://example.test:8080/ sent
        // 04 01 1f 90 00 00 00 01 00 65 78 61 6d 70 6c 65 2e 74 65 73 74 00
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4a, "example.test", Socks4Granted);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            (byte[])[0x04, 0x01, 0x1F, 0x90, 0x00, 0x00, 0x00, 0x01, 0x00, .. "example.test"u8, 0x00],
            proxyConnection.Written);
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("::1")]
    public async Task ConnectAsync_ThroughSocks4aToAnAddressLiteral_SendsTheLiteralAsAName(string host)
    {
        // curl -x socks4a://127.0.0.1:19080 http://127.0.0.1:8080/ sent 04 01 1f 90 00 00 00 01 00 31 32 37 2e 30 2e 30 2e 31 00
        var (_, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4a, host, Socks4Granted);

        Diagnostics.Assert("bytes sent to proxy", 9 + host.Length + 1, proxyConnection.Written.Count);
        CollectionAssert.AreEqual(
            (byte[])[0x04, 0x01, 0x1F, 0x90, 0x00, 0x00, 0x00, 0x01, 0x00, .. System.Text.Encoding.ASCII.GetBytes(host), 0x00],
            proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4aWithAUser_SendsTheUserIdBeforeTheName()
    {
        // curl -x socks4a://bob:pw@127.0.0.1:19080 http://example.test:8080/ sent
        // 04 01 1f 90 00 00 00 01 62 6f 62 00 65 78 61 6d 70 6c 65 2e 74 65 73 74 00
        var (_, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4a, "example.test", Socks4Granted, new NetworkCredential("bob", "pw"));

        Diagnostics.Assert("bytes sent to proxy", 25, proxyConnection.Written.Count);
        CollectionAssert.AreEqual(
            (byte[])[0x04, 0x01, 0x1F, 0x90, 0x00, 0x00, 0x00, 0x01, 0x62, 0x6F, 0x62, 0x00, .. "example.test"u8, 0x00],
            proxyConnection.Written);
    }

    [TestMethod]
    [DataRow(254, CurlExitCode.Ok)]
    [DataRow(255, CurlExitCode.Proxy)]
    public async Task ConnectAsync_ThroughSocks4a_RefusesANameOf255BytesOrMore(int length, CurlExitCode exitCode)
    {
        // A 254-byte name went through; 255 -> curl: (97) SOCKS4: too long hostname
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks4a, new string('a', length), Socks4Granted);

        Diagnostics.Assert("exit code", exitCode, result.ExitCode);

        Assert.AreEqual(exitCode, result.ExitCode);
        if (exitCode == CurlExitCode.Proxy)
        {
            AssertProxyFailure(result, proxyConnection, "SOCKS4: too long hostname");
            Assert.IsEmpty(proxyConnection.Written);
        }
    }

    // SOCKS5 and SOCKS5h

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5ToAnIPv4Literal_OffersNoAuthenticationAndGssapiThenConnects()
    {
        // curl -x socks5://127.0.0.1:19080 http://127.0.0.1:8080/ sent 05 02 00 01, then 05 01 00 01 7f 00 00 01 1f 90
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5NoAuthentication, .. Socks5Succeeded, .. BytesAfterTheHandshake]);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(proxyConnection, result.Connection);
        CollectionAssert.AreEqual(
            new byte[] { 0x05, 0x02, 0x00, 0x01, 0x05, 0x01, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0x1F, 0x90 },
            proxyConnection.Written);
        Assert.AreEqual(BytesAfterTheHandshake.Length, proxyConnection.UnreadCount);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5hToAName_SendsTheNameForTheProxyToResolve()
    {
        // curl -x socks5h://127.0.0.1:19080 http://example.test:8080/ sent 05 02 00 01, then
        // 05 01 00 03 0c 65 78 61 6d 70 6c 65 2e 74 65 73 74 1f 90
        var resolver = new FakeDnsResolver(ProxyAddress);
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5Hostname, "example.test", [.. Socks5NoAuthentication, .. Socks5Succeeded], resolver: resolver);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            (byte[])[0x05, 0x02, 0x00, 0x01, 0x05, 0x01, 0x00, 0x03, 0x0C, .. "example.test"u8, 0x1F, 0x90],
            proxyConnection.Written);
        CollectionAssert.AreEqual(new[] { "socks.example" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks5)]
    [DataRow(ProxyKind.Socks5Hostname)]
    public async Task ConnectAsync_ThroughSocks5ToAnIPv4Literal_SendsItAsAnAddress(ProxyKind kind)
    {
        // curl -x socks5h://127.0.0.1:19080 http://127.0.0.1:8080/ sent 05 01 00 01 7f 00 00 01 1f 90
        var (_, proxyConnection) = await ConnectThroughSocksAsync(kind, "127.0.0.1", [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x05, 0x01, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0x1F, 0x90 }, [.. proxyConnection.Written[4..]]);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0x1F, 0x90 }, proxyConnection.Written[4..]);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5ToAnIPv6Literal_SendsItAsAnIPv6Address()
    {
        // curl -x socks5://127.0.0.1:19080 http://[::1]:8080/ sent 05 01 00 04 00 .. 00 01 1f 90
        var (_, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5Hostname, "::1", [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Assert("bytes sent after the greeting", 22, proxyConnection.Written.Count - 4);
        CollectionAssert.AreEqual(
            new byte[] { 0x05, 0x01, 0x00, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0x1F, 0x90 },
            proxyConnection.Written[4..]);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5ToAName_SendsItsFirstResolvedAddress()
    {
        var (_, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "target.example",
            [.. Socks5NoAuthentication, .. Socks5Succeeded],
            resolveEntry: "target.example:8080:10.1.2.3,[::1]");

        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x05, 0x01, 0x00, 0x01, 10, 1, 2, 3, 0x1F, 0x90 }, [.. proxyConnection.Written[4..]]);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01, 0x00, 0x01, 10, 1, 2, 3, 0x1F, 0x90 }, proxyConnection.Written[4..]);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5ToAHostThatOnlyLooksNumeric_ResolvesItAsAName()
    {
        // "1" parses as 0.0.0.1, but it is not written the way an address prints, so it is a name.
        var (_, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "1",
            [.. Socks5NoAuthentication, .. Socks5Succeeded],
            resolveEntry: "1:8080:10.1.2.3");

        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x05, 0x01, 0x00, 0x01, 10, 1, 2, 3, 0x1F, 0x90 }, [.. proxyConnection.Written[4..]]);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01, 0x00, 0x01, 10, 1, 2, 3, 0x1F, 0x90 }, proxyConnection.Written[4..]);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5WithACredential_OffersUserNameAndPasswordAndSendsThem()
    {
        // curl -x socks5://bob:pw@127.0.0.1:19080 http://127.0.0.1:8080/ sent 05 03 00 01 02, then 01 03 62 6f 62 02 70 77
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [0x05, 0x02, 0x01, 0x00, .. Socks5Succeeded],
            new NetworkCredential("bob", "pw"));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new byte[] { 0x05, 0x03, 0x00, 0x01, 0x02, 0x01, 0x03, 0x62, 0x6F, 0x62, 0x02, 0x70, 0x77, 0x05, 0x01, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0x1F, 0x90 },
            proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5WithACredentialTheProxyDoesNotAskFor_SkipsTheSubNegotiation()
    {
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5NoAuthentication, .. Socks5Succeeded],
            new NetworkCredential("bob", "pw"));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x05, 0x03, 0x00, 0x01, 0x02, 0x05, 0x01 }, [.. proxyConnection.Written[..7]]);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x03, 0x00, 0x01, 0x02, 0x05, 0x01 }, proxyConnection.Written[..7]);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ProxyAsksForAPasswordThatWasNotGiven_SendsAnEmptyUserNameAndPassword()
    {
        // The proxy picked 02 after 05 02 00 01 -> curl sent 01 00 00
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", [0x05, 0x02, 0x01, 0x00, .. Socks5Succeeded]);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x05, 0x02, 0x00, 0x01, 0x01, 0x00, 0x00, 0x05 }, [.. proxyConnection.Written[..8]]);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x02, 0x00, 0x01, 0x01, 0x00, 0x00, 0x05 }, proxyConnection.Written[..8]);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ProxyRejectsTheUser_FailsWithProxy()
    {
        // The proxy answered 01 01 -> curl: (97) User was rejected by the SOCKS5 server (1 1).
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", [0x05, 0x02, 0x01, 0x01], new NetworkCredential("bob", "pw"));

        AssertProxyFailure(result, proxyConnection, "User was rejected by the SOCKS5 server (1 1).");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x05, 0x02, 0x01 }, DisplayName = "Mid-authentication status")]
    [DataRow(new byte[] { 0x05 }, DisplayName = "Mid-method choice")]
    [DataRow(new byte[] { 0x05, 0x00, 0x05, 0x00, 0x00, 0x01 }, DisplayName = "Mid-connect reply header")]
    [DataRow(new byte[] { 0x05, 0x00, 0x05, 0x00, 0x00, 0x01, 0x7F, 0x00 }, DisplayName = "Mid-connect reply address")]
    public async Task ConnectAsync_WhenTheSocks5ProxyClosesMidReply_FailsWithProxy(byte[] reply)
    {
        // The proxy answered 05 00 05 00 00 01 and closed -> curl: (97) Failed to receive SOCKS response, proxy closed connection
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", reply);

        AssertProxyFailure(result, proxyConnection, "Failed to receive SOCKS response, proxy closed connection");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x04, 0x01 }, "Received invalid version in initial SOCKS5 response.")]
    [DataRow(new byte[] { 0x05, 0xFF }, "No authentication method was acceptable.")]
    [DataRow(new byte[] { 0x05, 0x03 }, "Unknown SOCKS5 mode attempted to be used by server.")]
    public async Task ConnectAsync_WhenTheSocks5ProxyPicksNoUsableMethod_FailsWithProxy(byte[] choice, string message)
    {
        // The proxy answered 05 ff -> curl: (97) No authentication method was acceptable.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", choice);

        AssertProxyFailure(result, proxyConnection, message);
    }

    [TestMethod]
    [DataRow(256, 2, "Excessive username length for proxy auth")]
    [DataRow(3, 256, "Excessive password length for proxy auth")]
    public async Task ConnectAsync_ThroughSocks5WithACredentialOver255Bytes_FailsWithProxyAfterTheGreeting(int userLength, int passwordLength, string message)
    {
        // curl -x socks5://<256 x a>:pw@127.0.0.1:19080 ... sent 05 03 00 01 02 only -> curl: (97) Excessive username length for proxy auth
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [0x05, 0x02],
            new NetworkCredential(new string('a', userLength), new string('p', passwordLength)));

        AssertProxyFailure(result, proxyConnection, message);
        Diagnostics.Diff("bytes sent to proxy", new byte[] { 0x05, 0x03, 0x00, 0x01, 0x02 }, [.. proxyConnection.Written]);
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x03, 0x00, 0x01, 0x02 }, proxyConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5WithACredentialOf255Bytes_SendsIt()
    {
        var (result, _) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [0x05, 0x02, 0x01, 0x00, .. Socks5Succeeded],
            new NetworkCredential(new string('a', 255), new string('p', 255)));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    [DataRow(255, CurlExitCode.Ok)]
    [DataRow(256, CurlExitCode.Proxy)]
    public async Task ConnectAsync_ThroughSocks5h_RefusesANameOver255BytesBeforeTheGreeting(int length, CurlExitCode exitCode)
    {
        // A 255-byte name went through; 256 sent nothing ->
        // curl: (97) SOCKS5: the destination hostname is too long to be resolved remotely by the proxy.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5Hostname, new string('a', length), [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Assert("exit code", exitCode, result.ExitCode);

        Assert.AreEqual(exitCode, result.ExitCode);
        if (exitCode == CurlExitCode.Proxy)
        {
            AssertProxyFailure(result, proxyConnection, "SOCKS5: the destination hostname is too long to be resolved remotely by the proxy.");
            Assert.IsEmpty(proxyConnection.Written);
        }
    }

    [TestMethod]
    [DataRow((byte)1)]
    [DataRow((byte)9)]
    [DataRow((byte)255)]
    public async Task ConnectAsync_WhenTheSocks5ProxyCannotConnect_FailsWithProxyNamingTheHostAndStatus(byte status)
    {
        // The proxy answered 05 01 00 01 12 34 56 78 04 d2 -> curl: (97) cannot complete SOCKS5 connection to example.test. (1)
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5Hostname,
            "example.test",
            [.. Socks5NoAuthentication, 0x05, status, 0x00, 0x01, 0x12, 0x34, 0x56, 0x78, 0x04, 0xD2]);

        AssertProxyFailure(result, proxyConnection, $"cannot complete SOCKS5 connection to example.test. ({status})");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ConnectReplyHasTheWrongVersion_FailsWithProxy()
    {
        // The proxy answered 04 00 00 01 12 34 56 78 00 50 -> curl: (97) SOCKS5 reply has wrong version, version should be 5.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5NoAuthentication, 0x04, 0x00, 0x00, 0x01, 0x12, 0x34, 0x56, 0x78, 0x00, 0x50]);

        AssertProxyFailure(result, proxyConnection, "SOCKS5 reply has wrong version, version should be 5.");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ConnectReplyHasAnUnknownAddressType_FailsWithProxy()
    {
        // The proxy answered 05 00 00 09 00 00 00 00 00 00 -> curl: (97) SOCKS5 reply has wrong address type.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5NoAuthentication, 0x05, 0x00, 0x00, 0x09, 0, 0, 0, 0, 0, 0]);

        AssertProxyFailure(result, proxyConnection, "SOCKS5 reply has wrong address type.");
    }

    [TestMethod]
    [DataRow(new byte[] { 0x05, 0x00, 0x00, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0x00, 0x50 }, DisplayName = "IPv6")]
    [DataRow(new byte[] { 0x05, 0x00, 0x00, 0x03, 0x03, 0x61, 0x62, 0x63, 0x00, 0x50 }, DisplayName = "Host name")]
    public async Task ConnectAsync_WhenTheSocks5ConnectReplyBindsAnyAddressType_ReadsExactlyTheReply(byte[] reply)
    {
        // Measured: both replies opened the tunnel and the HTTP request followed.
        var (result, proxyConnection) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", [.. Socks5NoAuthentication, .. reply, .. BytesAfterTheHandshake]);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(BytesAfterTheHandshake.Length, proxyConnection.UnreadCount);
    }

    // The -v line

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks4a)]
    [DataRow(ProxyKind.Socks5)]
    [DataRow(ProxyKind.Socks5Hostname)]
    public async Task ConnectAsync_ThroughSocksToAName_ReportsTheOpenedSocksConnectionNamingTheHostAsGiven(ProxyKind kind)
    {
        // curl -sS -v -x socks5://127.0.0.1:41080 --resolve h.test:80:10.0.0.1 http://h.test/ (and socks4, socks4a, socks5h) ->
        // * Opened SOCKS connection from 127.0.0.1 port 60392 to h.test port 80 (via 127.0.0.1 port 41080) (BL-1038)
        byte[] reply = kind is ProxyKind.Socks4 or ProxyKind.Socks4a ? Socks4Granted : [.. Socks5NoAuthentication, .. Socks5Succeeded];
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection(reply) },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["socks.example:1080:192.0.2.10", "target.example:8080:10.1.2.3"]));

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("target.example", 8080, UseTls: false) { Proxy = new ProxyEndpoint(kind, "socks.example", 1080, null), Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.Contains(events.Info, "Opened SOCKS connection from 127.0.0.1 port 50000 to target.example port 8080 (via 192.0.2.10 port 1080)");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocksHandshakeFails_ReportsNoOpenedSocksConnection()
    {
        // curl -sS -v -x socks5h://127.0.0.1:41080 http://h.test/, the proxy answering 05 05 ... ->
        // * cannot complete SOCKS5 connection to h.test. (5), and no Opened SOCKS connection line (BL-1038)
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([.. Socks5NoAuthentication, 0x05, 0x05, 0x00, 0x01, 0x7F, 0x00, 0x00, 0x01, 0x1F, 0x90]) },
            new FakeTlsProvider(),
            new ManualTimeProvider());

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("h.test", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5Hostname, "socks.example", 1080, null), Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.Proxy, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Opened SOCKS connection", StringComparison.Ordinal)));
    }

    // Through the connector

    [TestMethod]
    public async Task ConnectAsync_ThroughSocksWithUseTls_RunsTlsOverTheTunnelForTheTargetHost()
    {
        var proxyConnection = new ScriptedConnection([.. Socks5NoAuthentication, .. Socks5Succeeded]);
        var tlsProvider = new FakeTlsProvider();
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), new FakeTcpDialer { DialOutcome = _ => proxyConnection }, tlsProvider, new ManualTimeProvider());

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("example.test", 443, UseTls: true) { Proxy = new ProxyEndpoint(ProxyKind.Socks5Hostname, "socks.example", 1080, null) });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(tlsProvider.SecuredConnection, result.Connection);
        Assert.AreSame(proxyConnection, tlsProvider.ReceivedPlaintext);
        Assert.AreEqual("example.test", tlsProvider.ReceivedTargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks_TakesConnectedWhenTheTunnelIsOpen()
    {
        var timeProvider = new ManualTimeProvider();
        var proxyConnection = new ScriptedConnection(Socks4Granted);
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ =>
            {
                timeProvider.Advance(5);
                return proxyConnection;
            },
        };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, new FakeTlsProvider(), timeProvider);

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("127.0.0.1", 8080, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks4, "socks.example", 1080, null) });

        Diagnostics.Assert("timings", new ConnectTimings(0, 0, 5, null), result.Timings);
        Assert.AreEqual(new ConnectTimings(0, 0, 5, null), result.Timings);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(ProxyAddress, 1080) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocksHandshakeThrows_DisposesTheConnectionAndRethrows()
    {
        var proxyConnection = new ScriptedConnection([]) { ReadException = new IOException("reset") };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), new FakeTcpDialer { DialOutcome = _ => proxyConnection }, new FakeTlsProvider(), new ManualTimeProvider());

        var exception = await Assert.ThrowsExactlyAsync<IOException>(
            async () => await ConnectLoggedAsync(
                connector,
                new ConnectTarget("127.0.0.1", 8080, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5, "socks.example", 1080, null) }));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("proxy connection disposed", true, proxyConnection.IsDisposed);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocksProxyRefuses_FailsWithCouldntConnectNamingTargetAndProxy()
    {
        // curl -x socks5://127.0.0.1:1 http://127.0.0.1:8080/ ->
        // curl: (7) Failed to connect to 127.0.0.1:8080 over proxy 127.0.0.1 after 2040 ms: Could not connect to server
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget("127.0.0.1", 8080, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5, "127.0.0.1", 1, null) });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 127.0.0.1:8080 over proxy 127.0.0.1 after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    private void AssertProxyFailure(ConnectResult result, ScriptedConnection proxyConnection, string message)
    {
        Diagnostics.Assert("exit code", CurlExitCode.Proxy, result.ExitCode);
        Diagnostics.Assert("error message", message, result.ErrorMessage);
        Diagnostics.Assert("exit code", CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.IsTrue(proxyConnection.IsDisposed);
    }

    // Connects to host:8080 through a SOCKS proxy at socks.example:1080 that answers with proxyReply.
    // The proxy resolves through --resolve, so the resolver answers only for the target.
    private async Task<(ConnectResult Result, ScriptedConnection ProxyConnection)> ConnectThroughSocksAsync(
        ProxyKind kind,
        string host,
        byte[] proxyReply,
        NetworkCredential? credential = null,
        string? resolveEntry = null,
        FakeDnsResolver? resolver = null,
        Socks5AuthenticationOptions? socks5Authentication = null,
        RecordingTransferEvents? events = null)
    {
        var proxyConnection = new ScriptedConnection(proxyReply);
        string[] entries = resolveEntry is null ? ["socks.example:1080:192.0.2.10"] : ["socks.example:1080:192.0.2.10", resolveEntry];
        var connector = new TcpConnector(
            resolver ?? new FakeDnsResolver(),
            new FakeTcpDialer { DialOutcome = _ => proxyConnection },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            resolveOverrides: resolver is null ? ResolveOverrides.Parse(entries) : null,
            socks5Authentication: socks5Authentication);

        Diagnostics.Arrange("proxy", $"{kind} at socks.example:1080, {(credential is null ? "no credential" : $"a {credential.UserName.Length}-character user name")}");
        Diagnostics.Bytes("proxy reply", proxyReply);
        var result = await ConnectLoggedAsync(
            connector,
            new ConnectTarget(host, 8080, UseTls: false) { Proxy = new ProxyEndpoint(kind, "socks.example", 1080, credential), Events = events ?? new RecordingTransferEvents() });
        Diagnostics.Bytes("sent to proxy", [.. proxyConnection.Written]);
        return (result, proxyConnection);
    }
}
