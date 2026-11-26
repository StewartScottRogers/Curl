using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that reports what became of a connection when an exchange ends.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Reports what became of the connection once it is disposed, as curl 8.21.0's <c>-v</c>
    /// does (ADR-0050): left intact when marked reusable, closed when the transfer failed, and
    /// shut down otherwise - its response did not persist, it died before its response, or only
    /// <c>--fail-with-body</c> failed it once its body was read. A transfer that ended only its
    /// HTTP/2 or HTTP/3 stream (<see cref="HttpAttemptOutcome.EndedOnlyItsStream" />) reports it
    /// left intact too, as curl 8.18.0's nghttp2 and ngtcp2 build does (measured, BL-5974,
    /// BL-5977, BL-5978, BL-5983 and BL-5996). When
    /// the request goes out again on a new connection - after it died, or as a retry such as the
    /// resend after a 417 (measured, BL-1446 Notes) - the line saying so follows. A connection left intact
    /// that another transfer still shares, on a stream of its own, is reported by the last
    /// transfer on it, not this one (BL-717). A resend <c>--max-redirs</c> refused is reported
    /// after the connection's end (<see cref="HttpAttemptOutcome.RefusedResendMessage" />).
    /// </summary>
    private static void ReportConnectionEnd(ITransferContext context, ConnectTarget target, ConnectResult connect, HttpAttemptOutcome outcome)
    {
        // curl 8.21.0 reports a multiplexed connection left intact only when its last transfer ends (measured, BL-717 Notes).
        if (!(SaysLeftIntact(outcome) && connect.Connection!.IsSharedWithAnotherTransfer))
        {
            context.Events.ReportInfo(ConnectionEndLine(target, connect, outcome));
            if (outcome.Retry is not null)
            {
                context.Events.ReportInfo(HttpConnectionInfoLines.IssueAnotherRequest(context.Url, OptionsOf(context).CommandLineTextEncoding));
            }
        }

        if (outcome.RefusedResendMessage is { } refused)
        {
            context.Events.ReportInfo(refused);
        }
    }

    /// <summary>
    /// Gives curl 8.21.0's <c>-v</c> line for what became of <paramref name="connect" />'s
    /// connection (ADR-0050): left intact, closing, or shutting down. A connection left intact is
    /// named by the target's host and port, or by its Unix domain socket (BL-794).
    /// </summary>
    private static string ConnectionEndLine(ConnectTarget target, ConnectResult connect, HttpAttemptOutcome outcome) =>
        outcome switch
        {
            _ when SaysLeftIntact(outcome) => LeftIntactLine(target, connect),
            { DiedBeforeResponse: false, FailedAfterBody: false } when ClosesTheConnection(outcome.Result.ExitCode) => HttpConnectionInfoLines.Closing(connect.ConnectionNumber),
            _ => HttpConnectionInfoLines.ShuttingDown(connect.ConnectionNumber),
        };

    /// <summary>
    /// Tells whether <c>-v</c> says the connection was left intact: it is one
    /// (<see cref="HttpAttemptOutcome.ReportsLeftIntact" />), or only its HTTP/2 or HTTP/3 stream
    /// ended (<see cref="HttpAttemptOutcome.EndedOnlyItsStream" />).
    /// </summary>
    private static bool SaysLeftIntact(HttpAttemptOutcome outcome) =>
        outcome.ReportsLeftIntact || outcome.EndedOnlyItsStream;

    /// <summary>
    /// Tells whether a failure that came once a response began ends with <c>closing connection
    /// #N</c>: every one but an empty reply, whose connection curl 8.22.0 shuts down (measured, BL-3617).
    /// </summary>
    private static bool ClosesTheConnection(CurlExitCode exitCode) =>
        exitCode is not (CurlExitCode.Ok or CurlExitCode.GotNothing);

    /// <summary>
    /// Names a connection left intact by the alt-svc alternative it was dialled to, else by the
    /// target's host and port: curl 8.22.0 names the URL's host and port even when <c>--connect-to</c>
    /// or <c>--unix-socket</c> dialled elsewhere (measured, BL-3741, CGR-212).
    /// </summary>
    private static string LeftIntactLine(ConnectTarget target, ConnectResult connect) =>
        target.AltSvcRoute switch
        {
            { Alternative: var alternative } => HttpConnectionInfoLines.LeftIntact(connect.ConnectionNumber, alternative.Host, alternative.Port),
            _ => HttpConnectionInfoLines.LeftIntact(connect.ConnectionNumber, target.Host, target.Port),
        };
}
