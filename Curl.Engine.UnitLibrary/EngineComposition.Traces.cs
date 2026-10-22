using HttpVersion = Curl.Api.Options.Http.HttpVersion;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>
    /// Whether <c>--trace-config</c> turned on curl 8.21.0's <c>[DNS]</c> lines: <c>dns</c>, <c>doh</c>,
    /// <c>network</c> (measured, BL-1103 Notes) and <c>all</c> each turn on the DNS filter's lines and the DoH resolver's alike (measured,
    /// BL-1102 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesDns(EngineTraceComponents traces) =>
        traces.Components.Contains("dns") || traces.Components.Contains("doh") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SETUP]</c> lines are written: <c>setup</c> or <c>all</c> is among the
    /// trace components, which <c>-vv</c> and up put there too (measured, BL-1103 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSetup(EngineTraceComponents traces) =>
        traces.Components.Contains("setup") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[HAPROXY]</c> lines are written: <c>haproxy</c>, <c>proxy</c> or
    /// <c>all</c> is among the trace components, which <c>-vvvv</c> puts there too; <c>network</c>
    /// does not turn them on (measured, BL-1160 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHaproxy(EngineTraceComponents traces) =>
        traces.Components.Contains("haproxy") || traces.Components.Contains("proxy") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SOCKS]</c> lines are written: <c>socks</c>, <c>proxy</c> or a
    /// <c>--trace-config all</c> is among the trace components; neither <c>network</c> nor the
    /// <c>all</c> that <c>-vvvv</c> puts there turns them on (measured, BL-1191 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSocks(EngineTraceComponents traces) =>
        traces.Components.Contains("socks")
        || traces.Components.Contains("proxy")
        || (traces.Components.Contains("all") && !traces.VerbosityComponents.Contains("all"));

    /// <summary>
    /// Whether curl 8.21.0's <c>[HTTP-PROXY]</c> lines are written: <c>http-proxy</c>, <c>proxy</c> or
    /// a <c>--trace-config all</c> is among the trace components; neither <c>network</c> nor the
    /// <c>all</c> that <c>-vvvv</c> puts there turns them on (measured, BL-1193 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHttpProxy(EngineTraceComponents traces) => TracesProxyComponent(traces, "http-proxy");

    /// <summary>
    /// Whether curl 8.21.0's <c>[H1-PROXY]</c> lines are written: <c>h1-proxy</c>, <c>proxy</c> or a
    /// <c>--trace-config all</c> is among the trace components; neither <c>network</c> nor the
    /// <c>all</c> that <c>-vvvv</c> puts there turns them on (measured, BL-1193 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesH1Proxy(EngineTraceComponents traces) => TracesProxyComponent(traces, "h1-proxy");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SSL]</c> lines are written around an origin's TLS handshake:
    /// <c>ssl</c>, <c>network</c> or <c>all</c> is among the trace components, which <c>-vvvv</c> puts
    /// there too; <c>proxy</c> and <c>tls</c> do not (measured, BL-1287 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSsl(EngineTraceComponents traces) =>
        traces.Components.Contains("ssl") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SSL-PROXY]</c> lines are written around an HTTPS proxy's TLS handshake:
    /// <c>proxy</c> or a <c>--trace-config all</c> is among the trace components; neither <c>ssl</c>,
    /// <c>network</c> nor the <c>all</c> that <c>-vvvv</c> puts there turns them on (measured, BL-1287 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSslProxy(EngineTraceComponents traces) =>
        traces.Components.Contains("proxy")
        || (traces.Components.Contains("all") && !traces.VerbosityComponents.Contains("all"));

    private static bool TracesProxyComponent(EngineTraceComponents traces, string component) =>
        traces.Components.Contains(component)
        || traces.Components.Contains("proxy")
        || (traces.Components.Contains("all") && !traces.VerbosityComponents.Contains("all"));

    /// <summary>
    /// Whether curl 8.21.0's <c>[HAPPY-EYEBALLS]</c> lines are written: <c>happy-eyeballs</c>,
    /// <c>network</c> or <c>all</c> is among the trace components, which <c>-vvvv</c> puts there too
    /// (measured, BL-1161 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHappyEyeballs(EngineTraceComponents traces) =>
        traces.Components.Contains("happy-eyeballs") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[TCP]</c> lines are written: <c>tcp</c>, <c>network</c> or <c>all</c> is
    /// among the trace components, which <c>-vvvv</c> puts there too (measured, BL-1161 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesTcp(EngineTraceComponents traces) =>
        traces.Components.Contains("tcp") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[TIMER]</c> lines are written: <c>timer</c>, <c>network</c> or <c>all</c>
    /// is among the trace components, which <c>-vvvv</c> puts there too (measured, BL-1186 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesTimer(EngineTraceComponents traces) =>
        traces.Components.Contains("timer") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[HTTPS-CONNECT]</c> lines are written: <c>https-connect</c> or <c>all</c>
    /// is among the trace components, which <c>-vvvv</c> puts there too; neither <c>network</c> nor
    /// <c>proxy</c> turns them on (measured, BL-1192 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHttpsConnect(EngineTraceComponents traces) =>
        traces.Components.Contains("https-connect") || traces.Components.Contains("all");

    /// <summary>
    /// The HTTP version curl 8.21.0's <c>[HTTPS-CONNECT]</c> filter names for its first attempt:
    /// <c>h1</c> under <c>--http1.0</c> and <c>--http1.1</c>, <c>h3</c> under <c>--http3</c> and
    /// <c>--http3-only</c>, else <c>h2</c> (measured for none and <c>--http1.1</c>, BL-1192 Notes).
    /// </summary>
    /// <param name="version">The option group's HTTP version, <see cref="HttpVersion.None" /> when no version option was given.</param>
    /// <returns>The version's name.</returns>
    internal static string HttpsConnectFirstAttemptVersionOf(HttpVersion version) =>
        version switch
        {
            HttpVersion.Http10 or HttpVersion.Http11 => "h1",
            HttpVersion.Http3 or HttpVersion.Http3Only => "h3",
            _ => "h2",
        };

    /// <summary>
    /// Whether curl 8.21.0's <c>[READ]</c> lines are written: <c>read</c> or <c>all</c> is among the
    /// trace components, which <c>-vvv</c> and up put there too; <c>network</c> does not turn them on
    /// (measured, BL-1103 and BL-1159 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesRead(EngineTraceComponents traces) =>
        traces.Components.Contains("read") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[MULTI]</c> transfer engine lines are written: <c>multi</c>, <c>network</c>
    /// or <c>all</c> is among the trace components, which <c>-vvvv</c> puts there too (measured, BL-1103
    /// and BL-1188 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesMulti(EngineTraceComponents traces) =>
        traces.Components.Contains("multi") || traces.Components.Contains("network") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[WRITE]</c> client writer lines are written: <c>write</c> or <c>all</c> is
    /// among the trace components, which <c>-vvv</c> and up put there too; <c>network</c> does not turn them
    /// on (measured, BL-1103 and BL-1187 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesWrite(EngineTraceComponents traces) =>
        traces.Components.Contains("write") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[FTP]</c> lines are written: <c>ftp</c>, <c>protocol</c> or
    /// <c>all</c> is among the trace components, which <c>-vv</c> and up put there too (measured,
    /// BL-1162 Notes).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesFtp(EngineTraceComponents traces) =>
        traces.Components.Contains("ftp") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SMTP]</c> lines are written: <c>smtp</c>, <c>protocol</c> or
    /// <c>all</c> is among the trace components, which <c>-vv</c> and up put there too (BL-1163).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSmtp(EngineTraceComponents traces) =>
        traces.Components.Contains("smtp") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[WS]</c> lines are written: <c>ws</c>, <c>protocol</c> or <c>all</c>
    /// is among the trace components, which <c>-vv</c> and up put there too (BL-1164).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesWs(EngineTraceComponents traces) =>
        traces.Components.Contains("ws") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[SSH]</c> lines are written: <c>ssh</c>, <c>protocol</c> or <c>all</c>
    /// is among the trace components, which <c>-vv</c> and up put there too (BL-1166).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesSsh(EngineTraceComponents traces) =>
        traces.Components.Contains("ssh") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[HTTP/2]</c> frame lines are written: <c>http/2</c>, <c>protocol</c> or
    /// <c>all</c> is among the trace components, which <c>-vv</c> and up put there too (BL-1167).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHttp2(EngineTraceComponents traces) =>
        traces.Components.Contains("http/2") || traces.Components.Contains("protocol") || traces.Components.Contains("all");

    /// <summary>
    /// Whether curl 8.21.0's <c>[HTTP/3]</c> stream lines are written: <c>http/3</c>, <c>protocol</c> or
    /// <c>all</c> is among the trace components, which <c>-vv</c> and up put there too (BL-1168).
    /// </summary>
    /// <param name="traces">The run's <c>--trace-config</c> components.</param>
    /// <returns><see langword="true" /> when the lines are written.</returns>
    internal static bool TracesHttp3(EngineTraceComponents traces) =>
        traces.Components.Contains("http/3") || traces.Components.Contains("protocol") || traces.Components.Contains("all");
}
