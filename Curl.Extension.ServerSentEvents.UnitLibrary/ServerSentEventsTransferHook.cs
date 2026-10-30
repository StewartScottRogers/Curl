using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ServerSentEvents;

/// <summary>
/// What <c>--sse</c> does to an HTTP transfer (ADR-1067). It asks for an event stream with the headers
/// §9.2.2 "The EventSource interface" fetches with, and <c>Last-Event-ID</c> once the stream has set one
/// (§9.2.4 "Processing model"); writes the response body as the events §9.2.6 dispatches
/// (<see cref="EventStreamWritingStream" />); and, when a <c>200</c> <c>text/event-stream</c> response ends,
/// asks for the transfer to run again after the reconnection time (§9.2.3 "Reestablishing the connection").
/// Any other response fails the connection: it is not repeated. A transfer that is not HTTP runs unchanged.
/// </summary>
internal sealed class ServerSentEventsTransferHook : ITransferHook
{
    /// <summary>The reconnection time before a <c>retry</c> field sets one, in milliseconds; §9.2.2 leaves it to the user agent.</summary>
    internal const long DefaultReconnectionTimeMilliseconds = 3000;

    /// <summary>The longest wait the host's timer accepts, in milliseconds; a longer <c>retry</c> waits this long.</summary>
    internal const long LongestReconnectionTimeMilliseconds = int.MaxValue;

    private const string EventStreamMediaType = "text/event-stream";

    private readonly EventStreamParser _parser = new();

    /// <inheritdoc />
    public IEnumerable<string> RequestHeadersFor(ITransferContext context)
    {
        List<string> headers = [$"Accept: {EventStreamMediaType}", "Cache-Control: no-cache", "Pragma: no-cache"];
        if (_parser.LastEventId.Length > 0)
        {
            headers.Add($"Last-Event-ID: {_parser.LastEventId}");
        }

        return headers;
    }

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        EventStreamWritingStream events = new(_parser, context.Output);
        try
        {
            return await next(context.WithOutputs(events, context.HeaderOutput, context.DumpHeaderOutput)).ConfigureAwait(false);
        }
        finally
        {
            _parser.EndStream();
        }
    }

    /// <inheritdoc />
    public TimeSpan? RepeatAfter(TransferResult result) =>
        result.Report is { ResponseCode: 200, ContentType: { } contentType } && IsEventStream(contentType)
            ? TimeSpan.FromMilliseconds(Math.Min(_parser.ReconnectionTimeMilliseconds ?? DefaultReconnectionTimeMilliseconds, LongestReconnectionTimeMilliseconds))
            : null;

    /// <summary>Whether a <c>Content-Type</c> value names <c>text/event-stream</c>, whatever its parameters and case.</summary>
    private static bool IsEventStream(string contentType) =>
        contentType.Split(';')[0].Trim().Equals(EventStreamMediaType, StringComparison.OrdinalIgnoreCase);
}
