using System.Net;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" /> through <c>--ntlm</c> and <c>--proxy-ntlm</c>
/// together, against a proxy that answers every request without <c>Proxy-Authorization</c> with
/// a bare <c>NTLM</c> <c>407</c> and an origin that never accepts the Type 3 message. The
/// requests are the ones curl 8.22.0 sent <c>Record-CurlExchange.ps1</c> for the same exchange
/// (BL-5892).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    /// <summary>
    /// The <c>407</c> that draws <c>Authorization: NTLM</c> Type 3 is answered without it, so the
    /// origin's next bare <c>NTLM</c> starts its handshake again from Type 1 instead of ending
    /// the transfer on the <c>401</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_OriginNtlmType3DrawsAProxy407_ResendsWithoutItAndStartsTheOriginHandshakeAgain()
    {
        const string originChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM " + ProxyNtlmType2 + "\r\nContent-Length: 0\r\n\r\n";
        const string originBareHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM\r\nContent-Length: 0\r\n\r\n";
        TurnTakingConnection connection = new(
            65536,
            ProxyNtlmChallengeHead,
            originChallengeHead,
            ProxyNtlmRejectedHead,
            ProxyNtlmChallengeHead,
            originBareHead,
            ProxyOkHead + "ok");
        SecurityContextStep type1 = new(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(ProxyNtlmType1));
        SecurityContextStep type3 = new(SecurityContextStatus.Completed, Convert.FromBase64String(ProxyNtlmType3));
        ScriptedTokenSource tokens = new(type1, type1, type1, type3, type1, type3, type1, type1, type3, type1);
        MemoryStream output = new();
        Diagnostics.Arrange("scripted responses", "407 type 2, 401 type 2, 407 bare, 407 type 2, 401 bare, 200 ok");

        TransferResult result = await ProxyTokenHandler(QueueConnector.For(connection), tokens, HttpAuthSchemes.Ntlm)
            .ExecuteAsync(ProxyChallengeContext(output, originCredential: new NetworkCredential("u", "p"), originSchemes: HttpAuthSchemes.Ntlm));

        const string proxyType1 = "Proxy-Authorization: NTLM " + ProxyNtlmType1 + "\r\n";
        const string proxyType3 = "Proxy-Authorization: NTLM " + ProxyNtlmType3 + "\r\n";
        const string originType1 = "Authorization: NTLM " + ProxyNtlmType1 + "\r\n";
        const string originType3 = "Authorization: NTLM " + ProxyNtlmType3 + "\r\n";
        string expected = string.Concat(
            new[] { proxyType1 + originType1, proxyType3 + originType1, originType3, proxyType1, proxyType3, originType1 }
                .Select(headers => ProxyRequestStart + headers + ProxyRequestEnd));
        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("requests written", OneLine(expected), OneLine(connection.Written));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, connection.Written);
        Assert.AreEqual("ok", Latin1(output.ToArray()));
    }
}
