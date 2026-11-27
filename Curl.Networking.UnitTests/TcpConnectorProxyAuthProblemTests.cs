using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// STD-227: a refused CONNECT whose <c>407</c> draws curl's <c>authproblem</c> (the challenge
/// offers the Basic or Digest scheme the CONNECT already sent) is marked
/// <see cref="ConnectResult.IsProxyAuthenticationProblem" />, so <c>-f</c> prints the generic
/// exit 22 text (measured with curl 8.22.0, BL-6024 Notes, ADR-1465).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_BasicRefusedByABasicChallenge_IsMarkedAProxyAuthenticationProblem()
    {
        var (connector, _) = CreateAuthenticatingConnector(HttpAuthSchemes.Basic, new ScriptedConnection(Encoding.Latin1.GetBytes(BasicChallengeClosing)));

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Assert.AreEqual(407, result.ProxyConnectResponseCode);
        Assert.IsTrue(result.IsProxyAuthenticationProblem);
    }

    [TestMethod]
    public async Task ConnectAsync_RefusedWith403_IsNotMarkedAProxyAuthenticationProblem()
    {
        var (connector, _) = CreateAuthenticatingConnector(
            HttpAuthSchemes.Basic,
            new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")));

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget);

        Assert.AreEqual(403, result.ProxyConnectResponseCode);
        Assert.IsFalse(result.IsProxyAuthenticationProblem);
    }

    [TestMethod]
    [DataRow(407, "Basic realm=\"p\"", "Basic YTpi", true, DisplayName = "Basic sent, Basic offered")]
    [DataRow(407, "Digest realm=\"p\", nonce=\"n\"", "Digest username=\"a\"", true, DisplayName = "Digest sent, Digest offered")]
    [DataRow(407, "Digest realm=\"p\", nonce=\"n\", stale=true", "Digest username=\"a\"", false, DisplayName = "Digest sent, nonce stale")]
    [DataRow(407, "Digest realm=\"p\", nonce=\"n\"", "Basic YTpi", false, DisplayName = "Basic sent, only Digest offered")]
    [DataRow(407, "Basic realm=\"p\"", null, false, DisplayName = "nothing sent")]
    [DataRow(407, "Basic realm=\"p\"", "NTLM TlRMTVNTUAAB", false, DisplayName = "NTLM sent")]
    [DataRow(403, "Basic realm=\"p\"", "Basic YTpi", false, DisplayName = "403")]
    public void DrawsAuthenticationProblem_IsCurlsAuthProblem(int statusCode, string challenge, string? authorization, bool expected)
    {
        Assert.AreEqual(expected, ConnectTunnelVerboseLines.DrawsAuthenticationProblem(statusCode, [challenge], authorization));
    }

    [TestMethod]
    public void DrawsAuthenticationProblem_NoChallenge_IsFalse()
    {
        Assert.IsFalse(ConnectTunnelVerboseLines.DrawsAuthenticationProblem(407, [], "Basic YTpi"));
    }
}
