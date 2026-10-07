using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins how <see cref="TcpConnector" /> hands each CONNECT reply head to events that write it
/// to the transfer's header output (<see cref="IConnectReplyHeadWritingEvents" />): curl 8.21.0
/// writes every complete head, a refusal's and a <c>407</c>'s too, and nothing of a reply it
/// gives up on (measured 2026-09-30, BL-613 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WhenTheEventsWriteConnectReplyHeads_GivesThemTheOpeningReplysHead()
    {
        // curl -p -x 127.0.0.1:18613 -i http://example.com/ wrote the reply head before the response's.
        const string reply = "HTTP/1.1 200 Connection established\r\nX-Proxy: yes\r\n\r\n";
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply + "HTTP/1.1 200 OK\r\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());
        var events = new HeadRecordingTransferEvents();

        var result = await ConnectLoggedAsync(connector, PlainTarget with { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { reply }, events.Heads);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheEventsWriteConnectReplyHeads_GivesThemARefusalsHead()
    {
        // curl -p -x 127.0.0.1:18613 -i http://example.com/ answered 403 wrote its head, then exit 7.
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 3\r\n\r\nno\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());
        var events = new HeadRecordingTransferEvents();

        var result = await ConnectLoggedAsync(connector, PlainTarget with { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "HTTP/1.1 403 Forbidden\r\nContent-Length: 3\r\n\r\n" }, events.Heads);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheEventsWriteConnectReplyHeads_GivesThemEachReplyOfAnAnsweredChallenge()
    {
        const string challenge = "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 6\r\n\r\n";
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(challenge + "denied" + EstablishedReply));
        var (connector, _) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, connection);
        var events = new HeadRecordingTransferEvents();

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget with { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { challenge, EstablishedReply }, events.Heads);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyClosesBeforeItsHeaderBlockEnds_GivesTheEventsNoHead()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());
        var events = new HeadRecordingTransferEvents();

        var result = await ConnectLoggedAsync(connector, PlainTarget with { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.IsEmpty(events.Heads);
    }
}
