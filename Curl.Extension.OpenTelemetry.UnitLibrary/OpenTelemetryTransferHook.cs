using System.Collections.Concurrent;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OpenTelemetry.Otlp;
using Curl.Extension.OpenTelemetry.TraceContext;

namespace Curl.Extension.OpenTelemetry;

/// <summary>
/// The <c>--otel-endpoint</c> transfer hook (ADR-1134). Every HTTP transfer gets a client span: the hook sends its
/// <c>traceparent</c> (W3C Trace Context Level 1, §3.2), times the transfer on its <see cref="ITransferContext.TimeProvider" />,
/// and exports the span to the OTLP/HTTP JSON traces endpoint. A <c>traceparent</c> <c>-H</c> already gave is left to it,
/// and the span takes its trace-id and parent-id when it is valid. A failed export is a warning and never changes the
/// transfer's result. A transfer that is not HTTP is left alone.
/// </summary>
/// <param name="endpoint">The OTLP/HTTP traces endpoint, used as given.</param>
/// <param name="fillRandom">Fills a span with random bytes for the trace-id and span-id.</param>
internal sealed class OpenTelemetryTransferHook(string endpoint, Action<Span<byte>> fillRandom) : ITransferHook
{
    /// <summary>The component the export warnings are logged under.</summary>
    internal const string LogComponent = "otel";

    // With -L the redirect follower calls the hook once per hop; the hops share one trace and one span (BL-5098).
    // Each transfer's chain waiting on its next hop is kept under its progress and events sinks, which the follower
    // copies unchanged into every hop and the tool gives every transfer of its own, so -Z transfers run through one
    // hook each keep their own trace-id and start (BL-5162).
    private readonly ConcurrentDictionary<(ITransferProgress Progress, ITransferEvents Events), FollowedChain> _chains = new();

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        context.Http is null ? next(context) : next(WithTraceParent(context, ChainOf(context), out _));

    public async ValueTask<TransferResult> ExecuteAsync(
        ITransferContext context, TransferHookServices services, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        WarnWhenEndpointIsNotAnHttpUrl(context);
        var chain = ChainOf(context);
        var sent = WithTraceParent(context, chain, out var traceParent);
        var start = chain?.Start ?? context.TimeProvider.GetUtcNow();
        var result = await next(sent);
        if (IsFollowedRedirect(context, result))
        {
            _chains[KeyOf(context)] = new FollowedChain(traceParent, start, context, result, services);
            return result;
        }

        _chains.TryRemove(KeyOf(context), out _);
        await ExportAsync(traceParent, context, result, start, services);
        return result;
    }

    /// <summary>
    /// Exports the span of a chain whose next hop the follower refused (BL-5162): the hook saw a redirect it expected
    /// to be called again for, and never was. The span carries the last response the hook saw and the chain's outcome.
    /// </summary>
    public async ValueTask TransferChainFinishedAsync(ITransferContext first, TransferResult result)
    {
        if (_chains.TryRemove(KeyOf(first), out var chain))
        {
            var ended = result with { Report = result.Report ?? chain.LastResult.Report };
            await ExportAsync(chain.TraceParent, chain.LastHop, ended, chain.Start, chain.Services);
        }
    }

    private async ValueTask ExportAsync(
        TraceParent traceParent, ITransferContext context, TransferResult result, DateTimeOffset start, TransferHookServices services)
    {
        var span = OtlpHttpClientSpan.For(traceParent, context, result, start, context.TimeProvider.GetUtcNow());
        var failure = await new OtlpHttpExporter(services.Connector).ExportAsync(endpoint, OtlpJsonWriter.Write(span), context.CancellationToken);
        if (failure is not null)
        {
            context.DiagnosticLog.Write(DiagnosticLogLevel.Warning, LogComponent, $"OTLP export to {endpoint} failed: {failure}");
        }
    }

    private void WarnWhenEndpointIsNotAnHttpUrl(ITransferContext context)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https"))
        {
            context.DiagnosticLog.Write(DiagnosticLogLevel.Warning, LogComponent, $"--otel-endpoint {endpoint} is not an http or https URL");
        }
    }

    private static (ITransferProgress Progress, ITransferEvents Events) KeyOf(ITransferContext context) =>
        (context.Progress, context.Events);

    private FollowedChain? ChainOf(ITransferContext context) =>
        context.Http!.RedirectsFollowed > 0 && _chains.TryGetValue(KeyOf(context), out var chain) ? chain : null;

    private static bool IsFollowedRedirect(ITransferContext context, TransferResult result) =>
        context.Http is { FollowRedirects: true } http && MayFollowAnother(http) && IsRedirect(result);

    private static bool MayFollowAnother(HttpRequestOptions http) =>
        http.MaxRedirects < 0 || http.RedirectsFollowed < http.MaxRedirects;

    private static bool IsRedirect(TransferResult result) =>
        result.Report is { ResponseCode: >= 300 and < 400, RedirectUrl: not null };

    private ITransferContext WithTraceParent(ITransferContext context, FollowedChain? chain, out TraceParent traceParent)
    {
        var given = GivenTraceParentValue(context.Http!.Headers);
        if (given is null)
        {
            traceParent = chain?.TraceParent ?? TraceParent.Fresh(fillRandom);
            return context.WithRequestHeaders([TraceParent.FieldName + ": " + traceParent.ToString()]);
        }

        traceParent = TraceParent.Parse(given) ?? TraceParent.Fresh(fillRandom);
        return context;
    }

    private static string? GivenTraceParentValue(IReadOnlyList<string> headers)
    {
        foreach (var header in headers)
        {
            if (header.StartsWith(TraceParent.FieldName + ":", StringComparison.OrdinalIgnoreCase))
            {
                return header[(TraceParent.FieldName.Length + 1)..];
            }
        }

        return null;
    }

    /// <summary>A transfer's redirect chain waiting on its next hop: its span's ids and start, and the last hop the hook saw.</summary>
    private sealed record FollowedChain(
        TraceParent TraceParent, DateTimeOffset Start, ITransferContext LastHop, TransferResult LastResult, TransferHookServices Services);
}
