using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that writes the -v lines about authentication.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Reports, just before the request is sent, what curl 8.21.0 writes while it picks the
    /// request's auth (measured, BL-843 and BL-954 Notes): <c>Proxy auth using ...</c> for a
    /// forward proxy, then the lines the authenticator reported while it made the request's
    /// <c>Authorization</c> value before any challenge, then <c>Server auth using ...</c>
    /// (<see cref="HttpAuthUsingLines" />).
    /// </summary>
    private static void ReportAuthorizationLines(HttpRequestPlan plan)
    {
        bool headerNamesAuthorization = plan.Options.AuthorizationHeaderWithheld || HttpRequestHeadFormatter.CustomHeadersOf(plan.Options.Headers, plan.Options).Any(header => header.Names("Authorization"));
        if (plan.ProxyAuthRequest is { } proxyRequest)
        {
            ReportInfoLines(plan.Context.Events, plan.ProxyAuthorizationInfoLines);
            ReportAuthUsing(plan.Context.Events, HttpAuthUsingLines.AuthUsing(proxyRequest, plan.ProxyAuthorization, plan.ProxyAuthorizationAnswersChallenge, headerNamesAuthorization, plan.ProxyNtlmType3WentOut));
        }

        ReportInfoLines(plan.Context.Events, plan.AuthorizationInfoLines);
        ReportAuthUsing(plan.Context.Events, HttpAuthUsingLines.AuthUsing(plan.AuthRequest, plan.Authorization, plan.AuthorizationAnswersChallenge, headerNamesAuthorization, plan.NtlmType3WentOut));
    }

    /// <summary>
    /// Reports <see cref="HttpNtlmInfoLines.AuthRestartedLine" /> before a bare <c>NTLM</c>
    /// challenge that starts the origin's or the proxy's handshake again
    /// (<see cref="HttpNtlmInfoLines.RestartsAuth" />, BL-5932).
    /// </summary>
    private static void ReportNtlmAuthRestart(HttpRequestPlan plan, HttpStatusLine statusLine, HttpResponseHeader header)
    {
        if (HttpNtlmInfoLines.RestartsAuth(plan.AuthRequest, plan.NtlmType3WentOut, statusLine, header)
            || (plan.ProxyAuthRequest is { } proxyRequest && HttpNtlmInfoLines.RestartsAuth(proxyRequest, plan.ProxyNtlmType3WentOut, statusLine, header)))
        {
            plan.Context.Events.ReportInfo(HttpNtlmInfoLines.AuthRestartedLine);
        }
    }

    /// <summary>
    /// Decides whether <paramref name="header" /> of a final head is a challenge the lines the
    /// authenticator reports while it answers are written before: a Negotiate one
    /// (<see cref="HttpNegotiateInfoLines.IsNegotiateChallenge" />), or an NTLM one to the origin's
    /// or the proxy's request (<see cref="HttpNtlmInfoLines.IsNtlmChallenge" />, BL-848).
    /// </summary>
    private static bool IsAuthChallenge(HttpRequestPlan plan, HttpStatusLine statusLine, HttpResponseHeader header) =>
        HttpNegotiateInfoLines.IsNegotiateChallenge(plan.AuthRequest, statusLine, header)
            || HttpNtlmInfoLines.IsNtlmChallenge(plan.AuthRequest, statusLine, header)
            || (plan.ProxyAuthRequest is { } proxyRequest && HttpNtlmInfoLines.IsNtlmChallenge(proxyRequest, statusLine, header));

    /// <summary>
    /// Reports the <c>authentication problem, ignoring.</c> lines curl writes just before a
    /// challenge header refusing the Basic, Bearer or Digest value the request sent to the proxy
    /// or the origin, and its duplicate Digest lines, each read by the head's own
    /// <see cref="HttpAuthProblemLines" /> (BL-1040, BL-1175).
    /// </summary>
    private static void ReportAuthProblemLines(
        HttpRequestPlan plan, HttpStatusLine statusLine, HttpResponseHeader header, HttpAuthProblemLines originProblems, HttpAuthProblemLines proxyProblems)
    {
        if (plan.ProxyAuthRequest is { } proxyRequest)
        {
            ReportInfoLines(plan.Context.Events, [.. proxyProblems.LinesBefore(proxyRequest, plan.ProxyAuthorization, statusLine, header)]);
        }

        ReportInfoLines(plan.Context.Events, [.. originProblems.LinesBefore(plan.AuthRequest, plan.Authorization, statusLine, header)]);
    }

    /// <summary>Reports <paramref name="line" />, or nothing when it is <see langword="null" />.</summary>
    private static void ReportAuthUsing(ITransferEvents events, string? line)
    {
        if (line is not null)
        {
            events.ReportInfo(line);
        }
    }

    /// <summary>
    /// Reports <c>Need to rewind upload for next request</c> when a 401 or 407 is answered with
    /// <paramref name="retry" /> and the request sent a body that is not empty, as curl 8.21.0
    /// writes it after the head's last header, before <c>Ignoring the response-body</c>, for a
    /// <c>-T</c> file, a <c>-F</c> form or <c>-d</c> data, and not for an empty one (measured,
    /// BL-2001 Notes).
    /// </summary>
    private static void ReportAuthRetryRewind(HttpRequestPlan plan, HttpResponseHead head, HttpRequestPlan? retry)
    {
        if (retry is not null && head.StatusLine.StatusCode is 401 or 407 && SentNonEmptyBody(plan))
        {
            plan.Context.Events.ReportInfo(HttpConnectionInfoLines.NeedToRewindUpload);
        }
    }

    /// <summary>
    /// Tells whether the request carries a body that is not known to be empty.
    /// </summary>
    private static bool SentNonEmptyBody(HttpRequestPlan plan) =>
        plan.Framing.Body is not null && plan.Framing.KnownLength != 0;

    /// <summary>Reports each of <paramref name="lines" /> to <paramref name="events" />, in order.</summary>
    private static void ReportInfoLines(ITransferEvents events, IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
        {
            events.ReportInfo(line);
        }
    }
}
