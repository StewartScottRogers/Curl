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

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next) =>
        context.Http is null ? next(context) : next(WithTraceParent(context, out _));

    public async ValueTask<TransferResult> ExecuteAsync(
        ITransferContext context, TransferHookServices services, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        WarnWhenEndpointIsNotAnHttpUrl(context);
        var sent = WithTraceParent(context, out var traceParent);
        var start = context.Http.RedirectsFollowed > 0 && _firstHop is { } first ? first.Start : context.TimeProvider.GetUtcNow();
        _firstHop = (traceParent, start);
        var result = await next(sent);
        if (IsFollowedRedirect(context, result))
        {
            return result;
        }

        _firstHop = null;
        var span = OtlpHttpClientSpan.For(traceParent, context, result, start, context.TimeProvider.GetUtcNow());
        var failure = await new OtlpHttpExporter(services.Connector).ExportAsync(endpoint, OtlpJsonWriter.Write(span), context.CancellationToken);
        if (failure is not null)
        {
            context.DiagnosticLog.Write(DiagnosticLogLevel.Warning, LogComponent, $"OTLP export to {endpoint} failed: {failure}");
        }

        return result;
    }

    private void WarnWhenEndpointIsNotAnHttpUrl(ITransferContext context)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https"))
        {
            context.DiagnosticLog.Write(DiagnosticLogLevel.Warning, LogComponent, $"--otel-endpoint {endpoint} is not an http or https URL");
        }
    }

    // With -L the redirect follower calls the hook once per hop; the hops share one trace and one span (BL-5098).
    // The hook keeps the first hop's ids and start, so it assumes one followed transfer at a time.
    private (TraceParent TraceParent, DateTimeOffset Start)? _firstHop;

    private static bool IsFollowedRedirect(ITransferContext context, TransferResult result) =>
        context.Http is { FollowRedirects: true } http && MayFollowAnother(http) && IsRedirect(result);

    private static bool MayFollowAnother(HttpRequestOptions http) =>
        http.MaxRedirects < 0 || http.RedirectsFollowed < http.MaxRedirects;

    private static bool IsRedirect(TransferResult result) =>
        result.Report is { ResponseCode: >= 300 and < 400, RedirectUrl: not null };

    private ITransferContext WithTraceParent(ITransferContext context, out TraceParent traceParent)
    {
        var given = GivenTraceParentValue(context.Http!.Headers);
        if (given is null)
        {
            if (context.Http.RedirectsFollowed > 0 && _firstHop is { } first)
            {
                traceParent = first.TraceParent;
                return context.WithRequestHeaders([TraceParent.FieldName + ": " + traceParent.ToString()]);
            }

            traceParent = TraceParent.Fresh(fillRandom);
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
}
