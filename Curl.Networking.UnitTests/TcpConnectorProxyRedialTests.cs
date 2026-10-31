using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through a proxy that closes the connection on a <c>407</c>,
/// so the answer goes out on a new connection ("Connect me again please"), and then answers that
/// CONNECT with a second <c>407</c>: the redialled CONNECT answers a challenge, so the second
/// <c>407</c> is the next leg of the handshake, not a first request's (AF-0299, BL-2937).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string BareNtlmChallengeClosing =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: NTLM\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    [TestMethod]
    public async Task ConnectAsync_WithProxyAnyAuth_WhenTheRedialledType1DrawsABareNtlm407_FailsWithThe407()
    {
        // The Type 1 on the new connection answered the first 407, so a bare NTLM 407 to it is
        // curl's "NTLM handshake failure (internal error)", not a cue to send Type 1 again.
        var events = new RecordingTransferEvents();
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(BareNtlmChallengeClosing));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes(BareNtlmChallengeClosing));
        var tokens = new ScriptedTokenSource(
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(TunnelNtlmType1)),
            new SecurityContextStep(SecurityContextStatus.ContinueNeeded, Convert.FromBase64String(TunnelNtlmType1)));
        var (connector, dialer) = CreateTokenConnector(HttpAuthSchemes.Any, tokens, matchesSspiBuild: false, first, second);

        var result = await ConnectLoggedAsync(connector, AuthenticatingTarget with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.AreEqual(UnauthenticatedConnect, Encoding.Latin1.GetString([.. first.Written]));
        Assert.AreEqual(TokenConnect("NTLM " + TunnelNtlmType1), Encoding.Latin1.GetString([.. second.Written]));
        Assert.HasCount(2, dialer.DialedEndPoints);
        Assert.AreEqual(1, events.Transcript.Count(line => line == "* Connect me again please"));
        Assert.AreEqual(1, events.Transcript.Count(line => line == "* NTLM handshake failure (internal error)"));
    }
}
