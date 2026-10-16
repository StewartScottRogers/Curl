using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <c>CURLPX_*</c> code each SOCKS4 and SOCKS5 handshake failure carries on its
/// <see cref="ConnectResult" />, the source of <c>CURLINFO_PROXY_ERROR</c>, as upstream's
/// <c>lib/socks.c</c> sets <c>data-&gt;info.pxcode</c> for it (BL-2375).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    [DataRow((byte)0x5B, CurlProxyCode.RequestFailed)]
    [DataRow((byte)0x5C, CurlProxyCode.Identd)]
    [DataRow((byte)0x5D, CurlProxyCode.IdentdDiffer)]
    [DataRow((byte)0x5E, CurlProxyCode.UnknownFail)]
    public async Task ConnectAsync_WhenTheSocks4ProxyRejects_CarriesTheRejectionsProxyError(byte code, CurlProxyCode expected)
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "127.0.0.1", [0x00, code, 0, 0, 0, 0, 0, 0]);

        AssertProxyError(expected, result);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks4ReplyHasTheWrongVersion_CarriesBadVersion()
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "127.0.0.1", [0x01, 0x5A, 0, 0, 0, 0, 0, 0]);

        AssertProxyError(CurlProxyCode.BadVersion, result);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks5)]
    public async Task ConnectAsync_WhenTheSocksProxyClosesMidReply_CarriesClosed(ProxyKind kind)
    {
        var (result, _) = await ConnectThroughSocksAsync(kind, "127.0.0.1", [0x05]);

        AssertProxyError(CurlProxyCode.Closed, result);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4WithAUserNameOf256Bytes_CarriesLongUser()
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "127.0.0.1", Socks4Granted, new NetworkCredential(new string('a', 256), string.Empty));

        AssertProxyError(CurlProxyCode.LongUser, result);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4aToANameOf255Bytes_CarriesLongHostname()
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks4a, new string('a', 255), Socks4Granted);

        AssertProxyError(CurlProxyCode.LongHostname, result);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks4ToAnIPv6Literal_CarriesResolveHost()
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks4, "::1", Socks4Granted);

        AssertProxyError(CurlProxyCode.ResolveHost, result);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSocks5hToANameOf256Bytes_CarriesLongHostname()
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks5Hostname, new string('a', 256), Socks5NoAuthentication);

        AssertProxyError(CurlProxyCode.LongHostname, result);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x04, 0x00 }, CurlProxyCode.BadVersion)]
    [DataRow(new byte[] { 0x05, 0xFF }, CurlProxyCode.NoAuthentication)]
    [DataRow(new byte[] { 0x05, 0x03 }, CurlProxyCode.UnknownMode)]
    public async Task ConnectAsync_WhenTheSocks5ProxyPicksNoUsableMethod_CarriesItsProxyError(byte[] choice, CurlProxyCode expected)
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", choice);

        AssertProxyError(expected, result);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x05, 0x02 }, CurlProxyCode.NoAuthentication)]
    [DataRow(new byte[] { 0x05, 0x01 }, CurlProxyCode.GssapiPerMessage)]
    public async Task ConnectAsync_WhenTheSocks5ProxyPicksAMethodNotEnabled_CarriesItsProxyError(byte[] choice, CurlProxyCode expected)
    {
        var allowed = expected == CurlProxyCode.NoAuthentication
            ? new Socks5AuthenticationOptions(AllowUserNameAndPassword: false, AllowGssapi: true)
            : new Socks5AuthenticationOptions(AllowUserNameAndPassword: true, AllowGssapi: false);

        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", choice, socks5Authentication: allowed);

        AssertProxyError(expected, result);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5GssapiNegotiationFails_CarriesGssapi()
    {
        var (result, _) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            Socks5PicksGssapi,
            socks5Authentication: Socks5AuthenticationOptions.Default with { UsesSspiTexts = true });

        AssertProxyError(CurlProxyCode.Gssapi, result);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocks5ProxyRejectsTheUser_CarriesUserRejected()
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", [0x05, 0x02, 0x01, 0x01], new NetworkCredential("bob", "pw"));

        AssertProxyError(CurlProxyCode.UserRejected, result);
    }

    [TestMethod]
    [DataRow(256, 2, CurlProxyCode.LongUser)]
    [DataRow(3, 256, CurlProxyCode.LongPassword)]
    public async Task ConnectAsync_ThroughSocks5WithACredentialOver255Bytes_CarriesItsProxyError(int userLength, int passwordLength, CurlProxyCode expected)
    {
        var (result, _) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [0x05, 0x02],
            new NetworkCredential(new string('a', userLength), new string('p', passwordLength)));

        AssertProxyError(expected, result);
    }

    [TestMethod]
    [DataRow((byte)1, CurlProxyCode.ReplyGeneralServerFailure)]
    [DataRow((byte)2, CurlProxyCode.ReplyNotAllowed)]
    [DataRow((byte)3, CurlProxyCode.ReplyNetworkUnreachable)]
    [DataRow((byte)4, CurlProxyCode.ReplyHostUnreachable)]
    [DataRow((byte)5, CurlProxyCode.ReplyConnectionRefused)]
    [DataRow((byte)6, CurlProxyCode.ReplyTtlExpired)]
    [DataRow((byte)7, CurlProxyCode.ReplyCommandNotSupported)]
    [DataRow((byte)8, CurlProxyCode.ReplyAddressTypeNotSupported)]
    [DataRow((byte)9, CurlProxyCode.ReplyUnassigned)]
    [DataRow((byte)255, CurlProxyCode.ReplyUnassigned)]
    public async Task ConnectAsync_WhenTheSocks5ProxyCannotConnect_CarriesTheReplysProxyError(byte status, CurlProxyCode expected)
    {
        var (result, _) = await ConnectThroughSocksAsync(
            ProxyKind.Socks5,
            "127.0.0.1",
            [.. Socks5NoAuthentication, 0x05, status, 0x00, 0x01, 0, 0, 0, 0, 0, 0]);

        AssertProxyError(expected, result);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x04, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 }, CurlProxyCode.BadVersion)]
    [DataRow(new byte[] { 0x05, 0x00, 0x00, 0x09, 0, 0, 0, 0, 0, 0 }, CurlProxyCode.BadAddressType)]
    public async Task ConnectAsync_WhenTheSocks5ConnectReplyIsMalformed_CarriesItsProxyError(byte[] reply, CurlProxyCode expected)
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", [.. Socks5NoAuthentication, .. reply]);

        AssertProxyError(expected, result);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheSocksTunnelOpens_CarriesOk()
    {
        var (result, _) = await ConnectThroughSocksAsync(ProxyKind.Socks5, "127.0.0.1", [.. Socks5NoAuthentication, .. Socks5Succeeded]);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("proxy error", CurlProxyCode.Ok, result.ProxyError);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlProxyCode.Ok, result.ProxyError);
    }

    private void AssertProxyError(CurlProxyCode expected, ConnectResult result)
    {
        Diagnostics.Assert("exit code", CurlExitCode.Proxy, result.ExitCode);
        Diagnostics.Assert("proxy error", expected, result.ProxyError);
        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(expected, result.ProxyError);
    }
}
