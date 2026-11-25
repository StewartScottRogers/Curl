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
    public async Task ConnectAsync_ThroughATunnelWhoseReplyHolds3Headers_CarriesTheCountInTheResult()
    {
        const string reply = "HTTP/1.1 200 Connection established\r\nX-A: 1\r\nX-B: 2\r\nX-C: 3\r\n\r\n";
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("result.ExitCode", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("connect reply headers stored", 3, result.ConnectReplyHeadersStored);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.ConnectReplyHeadersStored);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughATunnelOpenedByOneReply_CarriesItsHeadsBytesInTheResult()
    {
        // Upstream test1288's proxy reply: 61 bytes that curl 8.21.0 adds to %{size_header}.
        const string reply = "HTTP/1.1 200 Mighty fine indeed\r\nServer: test tunnel 2000\r\n\r\n";
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply + "HTTP/1.1 200 OK\r\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("proxy connect header bytes", 61L, result.ProxyConnectHeaderBytes);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(61L, result.ProxyConnectHeaderBytes);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughATunnelOpenedAfterA407_CarriesBothRepliesHeadBytesInTheResult()
    {
        const string challenge = "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 6\r\n\r\n";
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(challenge + "denied" + EstablishedReply));
        var (connector, _) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, connection);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        long expected = challenge.Length + EstablishedReply.Length;
        Diagnostics.Assert("proxy connect header bytes", expected, result.ProxyConnectHeaderBytes);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, result.ProxyConnectHeaderBytes);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAClosingChallengeIsFollowedByAConnectionTheProxyClosesUnanswered_KeepsTheChallengesStatusHeadersAndBytes()
    {
        // STD-168: --proxy-anyauth against a proxy that answers 407 and closes, every time: curl prints [56 407 2 N].
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(DigestChallengeClosing));
        var second = new ScriptedConnection([]);
        var (connector, _) = CreateAuthenticatingConnector(HttpAuthSchemes.Digest, first, second);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Diagnostics.Assert("exit code, status, headers, bytes", (CurlExitCode.RecvError, 407, 3, (long)DigestChallengeClosing.Length), (result.ExitCode, result.ProxyConnectResponseCode, result.ConnectReplyHeadersStored, result.ProxyConnectHeaderBytes));
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(407, result.ProxyConnectResponseCode);
        Assert.AreEqual(3, result.ConnectReplyHeadersStored);
        Assert.AreEqual((long)DigestChallengeClosing.Length, result.ProxyConnectHeaderBytes);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyRefusesTheConnect_CarriesTheRepliesHeaderCountAndBytesInTheFailure()
    {
        // STD-144: curl prints [7 2 59] for %{exitcode} %{num_headers} %{size_header} after this reply.
        const string reply = "HTTP/1.1 403 Forbidden\r\nContent-Length: 4\r\nX-Proxy: yes\r\n\r\nnope";
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        var result = await ConnectLoggedAsync(connector, PlainTarget);

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(2, result.ConnectReplyHeadersStored);
        Assert.AreEqual(59L, result.ProxyConnectHeaderBytes);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHandshakeInsideTheTunnelFails_ReturnsTheFailureWithNoHeaderCount()
    {
        const string reply = "HTTP/1.1 200 Connection established\r\nX-A: 1\r\n\r\n";
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        var tlsProvider = new FakeTlsProvider { FailureToReturn = ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake refused") };
        var connector = CreateProxyConnector(proxyConnection, tlsProvider);

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpProxy });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.IsNull(result.Connection);
        Assert.AreEqual(0, result.ConnectReplyHeadersStored);
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
