using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that chooses the connection or HTTP/2 or HTTP/3 stream an exchange runs over, including the h2c upgrade.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Marks an HTTP/3 request stream's transfer done with its session and writes its
    /// <c>--trace-config http/3</c> lines (<see cref="Http3StreamConnection.ReportTransferDone" />); other streams need neither.
    /// </summary>
    private static void ReportTransferDone(IHttpStreamConnection? requestStream, ConnectResult connect) =>
        (requestStream as Http3StreamConnection)?.ReportTransferDone(connect.ConnectionNumber);

    /// <summary>
    /// Gives the session the exchange ran on: <paramref name="streams" /> when it began on one,
    /// else the HTTP/2 session its connection switched to after an h2c upgrade's <c>101</c>
    /// (BL-866), else <see langword="null" /> for HTTP/1.x.
    /// </summary>
    private static IHttpStreamSession? StreamSessionAfter(IHttpStreamSession? streams, IConnection connection) =>
        streams ?? UpgradedSessionOf(connection);

    /// <summary>
    /// Gives the HTTP/2 session a connection switched to after an h2c upgrade's <c>101</c>, or
    /// <see langword="null" /> when it did not switch.
    /// </summary>
    private static Http2Session? UpgradedSessionOf(IConnection connection) =>
        (connection as HttpH2cUpgradeConnection)?.UpgradedSession;

    /// <summary>
    /// Gives the stream whose end is read for the response's trailers: the exchange's HTTP/2 or
    /// HTTP/3 stream, else stream 1 of a connection that switched to HTTP/2 after an h2c
    /// upgrade's <c>101</c>, so it is read to its end before the connection carries another
    /// request (BL-866), else <see langword="null" />.
    /// </summary>
    private static IHttpStreamConnection? TrailerStreamOf(IHttpStreamConnection? requestStream, IConnection connection) =>
        requestStream ?? (connection as HttpH2cUpgradeConnection)?.UpgradedStream;

    /// <summary>
    /// Creates the HTTP/2 or HTTP/3 stream the exchange runs on, with the request body's length (0 for
    /// none), reporting curl's <c>OPENED stream</c> lines once it is opened (<see cref="HttpStreamOpenedLines" />),
    /// or gives <see langword="null" /> when the connection speaks HTTP/1.x. An HTTP/2 stream is given
    /// <see cref="Http2PushSink" />, so the pushes promised on it are offered to it (BL-2516).
    /// </summary>
    private IHttpStreamConnection? CreateRequestStream(HttpRequestPlan plan, IHttpStreamSession? streams)
    {
        IHttpStreamConnection? stream = streams?.CreateStream(
            plan.Context.Url.Scheme,
            plan.Framing.Body is null ? 0 : plan.Framing.KnownLength,
            plan.Context.NoBody,
            new HttpStreamOpenedLines(plan.Context.Events, HttpUrlText.Effective(plan.Context.Url)),
            plan.Context.DiagnosticLog,
            TracesStreamsOf(streams) ? plan.Context.Events : null);
        (stream as Http2StreamConnection)?.PushSink = Http2PushSink;
        (stream as Http2StreamConnection)?.ReceiveBytesPerSecond = plan.Options.ReceiveBytesPerSecond;
        return stream;
    }

    /// <summary>
    /// Whether the session's version is traced: <see cref="TracesHttp3Streams" /> for HTTP/3,
    /// <see cref="TracesHttp2Frames" /> for HTTP/2.
    /// </summary>
    private bool TracesStreamsOf(IHttpStreamSession streams) =>
        streams is Http3Session ? TracesHttp3Streams : TracesHttp2Frames;

    /// <summary>
    /// Whether the request body's reads write <see cref="TracesClientReaders" />' lines: only over
    /// HTTP/1.x, without a <paramref name="requestStream" />, the version they were measured on.
    /// </summary>
    private bool TracesReadersOf(IHttpStreamConnection? requestStream) => TracesClientReaders && requestStream is null;

    /// <summary>
    /// Gives the connection the exchange reads and writes: the HTTP/2 or HTTP/3 stream when there is one;
    /// else, for <see cref="HttpVersionPreference.Http2" /> over cleartext, the transport watched for an
    /// h2c upgrade's <c>101</c> (<see cref="HttpH2cUpgradeConnection" />, BL-716); and else the transport itself.
    /// The upgrade's HTTP/2 lines are traced as <see cref="TracesHttp2Frames" /> says (BL-1205).
    /// </summary>
    private IConnection ExchangeConnectionOf(HttpRequestPlan plan, IHttpStreamConnection? requestStream, IConnection transport) =>
        (IConnection?)requestStream ?? (UpgradesToH2c(plan, transport)
            ? new HttpH2cUpgradeConnection(transport, plan.Context.Events, plan.Context.Url.Scheme, TracesHttp2Frames ? plan.Context.Events : null, plan.Context.TimeProvider)
            : transport);

    private static void EchoResponseLineAfter(IHttpStreamConnection? requestStream, IConnection connection, byte[] line)
    {
        IHttpStreamConnection? stream = TrailerStreamOf(requestStream, connection);
        (stream as Http2StreamConnection)?.EchoResponseLine(line);
        (stream as Http3StreamConnection)?.EchoResponseLineAfter(line);
    }

    /// <summary>
    /// Decides whether an HTTP/1.x exchange asks to upgrade to h2c: for <c>--http2</c>
    /// (<see cref="HttpVersionPreference.Http2" />) on a connection without TLS, as curl does
    /// (measured, BL-716 Notes).
    /// </summary>
    private static bool UpgradesToH2c(HttpRequestPlan plan, IConnection transport) =>
        plan.Options.Version == HttpVersionPreference.Http2 && !transport.IsSecure;

    /// <summary>
    /// Gives the plan the exchange sends on <paramref name="connection" />: over an h2c upgrade
    /// (<see cref="HttpH2cUpgradeConnection" />) the request without curl's own
    /// <c>Expect: 100-continue</c> (<see cref="HttpRequestFraming.ForH2cUpgrade" />), and
    /// otherwise the plan unchanged.
    /// </summary>
    private static HttpRequestPlan PlanForConnection(HttpRequestPlan plan, IConnection connection) =>
        connection is HttpH2cUpgradeConnection ? plan.ForH2cUpgrade() : plan;

    /// <summary>
    /// Tells whether the exchange's connection has switched to HTTP/2 after an h2c upgrade's <c>101</c>.
    /// </summary>
    private static bool IsSwitchedToHttp2(IConnection connection) =>
        connection is HttpH2cUpgradeConnection { IsUpgraded: true };
}
