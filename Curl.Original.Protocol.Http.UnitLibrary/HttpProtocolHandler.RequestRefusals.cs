using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that refuses a request, before it is sent, that curl cannot build.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Fails a request whose <c>-z</c> time curl 8.21.0 on Windows cannot write into its header
    /// (<see cref="HttpTimeConditionLimit" />): exit 43 before any byte is sent, the connection
    /// left intact, as measured (BL-381 Notes).
    /// </summary>
    /// <summary>
    /// Tells whether the request goes to a forward proxy in absolute form for a host whose wire
    /// form (<see cref="HttpUrlText.WireHost" />) curl cannot put in that target
    /// (<see cref="RefusesAbsoluteFormHost(string)" />).
    /// </summary>
    private static bool RefusesAbsoluteFormHost(HttpRequestPlan plan) =>
        plan.ForwardProxy is not null && RefusesAbsoluteFormHost(HttpUrlText.WireHost(plan.Context.Url));

    /// <summary>
    /// Tells whether <paramref name="wireHost" /> holds a character curl's
    /// <c>curl_url_set(CURLUPART_HOST)</c> refuses (<see cref="HostCharactersUrlSetRefuses" />), as a
    /// UTS 46 mapping can leave one (<c>a℀b.test</c> is <c>aa/cb.test</c>, BL-4081). An IPv6
    /// literal, the only host with a <c>:</c>, is never refused: curl parses it apart, zone and all.
    /// </summary>
    internal static bool RefusesAbsoluteFormHost(string wireHost) =>
        !wireHost.Contains(':', StringComparison.Ordinal) && wireHost.AsSpan().ContainsAny(HostCharactersUrlSetRefuses);

    /// <summary>
    /// The characters curl 8.18.0's <c>hostname_check</c> refuses in a host given to
    /// <c>curl_url_set(CURLUPART_HOST)</c>.
    /// </summary>
    private static readonly System.Buffers.SearchValues<char> HostCharactersUrlSetRefuses =
        System.Buffers.SearchValues.Create(" \r\n\t/:#?!@{}[]\\$'\"^`*<>=;,+&()%");

    /// <summary>
    /// Fails a request whose absolute-form target curl cannot build
    /// (<see cref="RefusesAbsoluteFormHost(HttpRequestPlan)" />) as curl 8.18.0 does once the proxy connection is
    /// open: <c>curl_url_set</c> fails, <c>http_target</c> answers <c>CURLE_OUT_OF_MEMORY</c> (exit 27,
    /// <c>Out of memory</c>) and no byte reaches the proxy (measured with libidn2 2.3.8, BL-4077).
    /// </summary>
    private static HttpAttemptOutcome AbsoluteFormTargetUnbuilt(HttpRequestPlan plan, ConnectResult connect)
    {
        TransferReport report = new()
        {
            UsedProxy = true,
            LocalEndPoint = connect.LocalEndPoint,
            Timings = new TransferTimings(plan.Started, connect.Timings, null, null, null, plan.Context.TimeProvider.GetTimestamp()),
        };
        TransferResult failed = TransferResult.Failure(CurlExitCode.OutOfMemory, "Out of memory") with { Report = report };
        return new HttpAttemptOutcome(failed, null, KeepsAlive: false);
    }

    private static HttpAttemptOutcome TimeValueRefused(HttpRequestPlan plan, ConnectResult connect)
    {
        TransferReport report = new()
        {
            UsedProxy = plan.Options.ForwardProxy is not null,
            LocalEndPoint = connect.LocalEndPoint,
            Timings = new TransferTimings(plan.Started, connect.Timings, null, null, null, plan.Context.TimeProvider.GetTimestamp()),
        };
        TransferResult refused = TransferResult.Failure(CurlExitCode.BadFunctionArgument, HttpTimeConditionLimit.InvalidTimeValue) with { Report = report };
        return new HttpAttemptOutcome(refused, null, KeepsAlive: true);
    }
}
