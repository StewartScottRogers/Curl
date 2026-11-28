using Curl.Protocol.Abstractions;

namespace Curl.Networking;

public sealed partial class TcpConnector
{
    // The failure of a CONNECT the proxy did not open: the reply's own failure, or the refusal,
    // marked IsProxyAuthenticationProblem when the 407 drew curl's authproblem (STD-227).
    private static ConnectResult TunnelFailure(HttpProxyTunnelReply reply, string? proxyAuthorization, string? firstSspiFailure, ConnectTarget target, long headBytes, (int StatusCode, int HeaderCount) lastStatusReply)
    {
        if (reply.FailureMessage is { } failureMessage)
        {
            return ConnectResult.Failed(reply.FailureExitCode, firstSspiFailure ?? failureMessage, null, 0, lastStatusReply.StatusCode, connectReplyHeadersStored: lastStatusReply.HeaderCount, proxyConnectHeaderBytes: lastStatusReply.StatusCode > 0 ? headBytes : 0);
        }

        var refused = $"CONNECT tunnel failed, response {reply.StatusCode}";
        var authenticationProblem = ConnectTunnelVerboseLines.DrawsAuthenticationProblem(reply.StatusCode, reply.ProxyAuthenticate, proxyAuthorization);
        ReportRefusal(target, reply.StatusCode, refused, authenticationProblem);
        return ConnectResult.Failed(CurlExitCode.CouldntConnect, firstSspiFailure ?? refused, null, 0, reply.StatusCode, connectReplyHeadersStored: reply.HeaderCount, proxyConnectHeaderBytes: headBytes)
            .WithProxyAuthenticationProblem(authenticationProblem);
    }

    // curl 8.22.0's -v line after a refused CONNECT's reply head: the refusal, unless -f fails the
    // transfer on the status first, which names the status, or says nothing after an authentication
    // problem (measured, BL-6049 Notes, ADR-1467).
    private static void ReportRefusal(ConnectTarget target, int statusCode, string refused, bool authenticationProblem)
    {
        if (!target.FailsOnHttpErrorStatus || statusCode < 400)
        {
            target.Events.ReportInfo(refused);
        }
        else if (!authenticationProblem)
        {
            target.Events.ReportInfo($"The requested URL returned error: {statusCode}");
        }
    }
}
