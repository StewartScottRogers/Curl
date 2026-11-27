using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    // The failure of a CONNECT the proxy did not open: the reply's own failure, or the refusal,
    // marked IsProxyAuthenticationProblem when the 407 drew curl's authproblem (STD-227).
    private static ConnectResult TunnelFailure(HttpProxyTunnelReply reply, string? proxyAuthorization, string? firstSspiFailure, ITransferEvents events, long headBytes, (int StatusCode, int HeaderCount) lastStatusReply)
    {
        if (reply.FailureMessage is { } failureMessage)
        {
            return ConnectResult.Failed(reply.FailureExitCode, firstSspiFailure ?? failureMessage, null, 0, lastStatusReply.StatusCode, connectReplyHeadersStored: lastStatusReply.HeaderCount, proxyConnectHeaderBytes: lastStatusReply.StatusCode > 0 ? headBytes : 0);
        }

        var refused = $"CONNECT tunnel failed, response {reply.StatusCode}";
        events.ReportInfo(refused);
        return ConnectResult.Failed(CurlExitCode.CouldntConnect, firstSspiFailure ?? refused, null, 0, reply.StatusCode, connectReplyHeadersStored: reply.HeaderCount, proxyConnectHeaderBytes: headBytes)
            .WithProxyAuthenticationProblem(ConnectTunnelVerboseLines.DrawsAuthenticationProblem(reply.StatusCode, reply.ProxyAuthenticate, proxyAuthorization));
    }
}
