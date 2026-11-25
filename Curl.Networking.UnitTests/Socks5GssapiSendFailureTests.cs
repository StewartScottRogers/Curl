using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// BL-5430: a write that fails during SOCKS5 GSS-API negotiation is exit 97 with
/// <c>CURLPX_GSSAPI</c>, the failed step's own send text and the GSS-API verbose lines. The texts
/// follow curl's <c>socks_gssapi.c</c> and <c>socks_sspi.c</c> (no GSS-API proxy was at hand to measure).
/// </summary>
[TestClass]
public sealed class Socks5GssapiSendFailureTests
{
    // Greeting offering only GSS-API: 05 01 01.
    private const int GreetingLength = 3;

    private static readonly byte[] PicksGssapi = [0x05, 0x01];

    private static async Task<(ConnectResult Result, RecordingTransferEvents Events)> ConnectAsync(
        SecurityContextStep step,
        int bytesWrittenBeforeReset,
        bool usesSspi)
    {
        var connection = new ScriptedConnection(PicksGssapi)
        {
            ExceptionOnWrite = new IOException("reset", new SocketException((int)SocketError.ConnectionReset)),
            BytesWrittenBeforeException = bytesWrittenBeforeReset,
        };
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(System.Net.IPAddress.Parse("192.0.2.10")),
            new FakeTcpDialer { DialOutcome = _ => connection },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            socks5Authentication: new Socks5AuthenticationOptions(false, true)
            {
                SecurityContexts = new ScriptedSecurityContextFactory(step),
                UsesSspiTexts = usesSspi,
            });

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.test", 80, UseTls: false)
            {
                Proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy.example", 1080, null),
                Events = events,
            },
            CancellationToken.None);
        return (result, events);
    }

    [TestMethod]
    [DataRow(false, "GSS-API")]
    [DataRow(true, "SSPI")]
    public async Task ConnectAsync_ResetWhileSendingTheAuthenticationToken_IsGssapiWithTheAuthenticationText(bool usesSspi, string api)
    {
        var (result, events) = await ConnectAsync(new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0xA1]), GreetingLength, usesSspi);

        var message = $"Failed to send {api} authentication request.";
        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(CurlProxyCode.Gssapi, result.ProxyError);
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { message, "Unable to negotiate SOCKS5 GSS-API context." }, events.Info[^2..]);
    }

    [TestMethod]
    [DataRow(false, "GSS-API")]
    [DataRow(true, "SSPI")]
    public async Task ConnectAsync_ResetWhileSendingTheProtectionLevel_IsGssapiWithTheEncryptionText(bool usesSspi, string api)
    {
        var (result, events) = await ConnectAsync(new SecurityContextStep(SecurityContextStatus.Completed, []), GreetingLength, usesSspi);

        var message = $"Failed to send {api} encryption request.";
        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(CurlProxyCode.Gssapi, result.ProxyError);
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { message, "Unable to negotiate SOCKS5 GSS-API context." }, events.Info[^2..]);
    }
}
