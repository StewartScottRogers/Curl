using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.HttpCache;

/// <summary>
/// The <c>--cache-dir</c> transfer hook (ADR-1069), a private cache (RFC 9111 §1). A cacheable GET whose
/// stored response is fresh (§4.2) is answered from the directory without contacting the origin. Otherwise
/// the request goes out, conditional on the stored response's validators when there is one (§4.3.1); a 304
/// that selects it (§4.3.3) updates it (§4.3.4) and the stored response is written in the 304's place; any
/// other response is written as received and stored when §3 allows and it has no <c>Vary</c> (§4.1).
/// A cache that cannot be written leaves the transfer as it was.
/// </summary>
/// <param name="directory">Where the responses are stored.</param>
internal sealed class HttpCacheTransferHook(CacheDirectory directory) : ITransferHook
{
    private const int NotModified = 304;

    private static readonly ResponseStorability Storability = new(isSharedCache: false);

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (CacheableRequest.TargetUriOf(context) is not { } target)
        {
            return await next(context);
        }

        var key = CacheKey.For("GET", target);
        var requestHeaders = CacheableRequest.HeaderFieldsOf(context.Http!);
        var stored = await directory.ReadAsync(key, context.CancellationToken);
        if (stored is not null && IsFreshFor(stored, requestHeaders, context.TimeProvider))
        {
            return Answered(stored, await StoredResponseOutput.WriteAsync(stored, context), context);
        }

        return await FetchAsync(new CacheLookup(key, requestHeaders, stored), context, next);
    }

    private static bool IsFreshFor(StoredResponse stored, IReadOnlyList<KeyValuePair<string, string>> requestHeaders, TimeProvider timeProvider) =>
        !CacheControlDirectives.Parse(requestHeaders).Has("no-cache")
        && new FreshnessCalculator(timeProvider, isSharedCache: false).Evaluate(stored) == CacheEntryUsability.Fresh;

    private static TransferResult Answered(StoredResponse stored, long bodyLength, ITransferContext context) =>
        TransferResult.Success(bodyLength) with
        {
            Report = new TransferReport
            {
                ResponseCode = stored.StatusCode,
                ResponseHeaders = stored.Headers,
                Method = "GET",
                EffectiveUrl = context.Url.OriginalString,
                ContentType = HttpHeaderValues.FindFirst(stored.Headers, "Content-Type"),
            },
        };

    private static bool SelectsStored(StoredResponse stored, TransferResult result) =>
        result is { IsSuccess: true, Report: { ResponseCode: NotModified } report }
        && CacheValidation.SelectsStoredResponse(stored, report.ResponseHeaders);

    private static bool IsStorable(IReadOnlyList<KeyValuePair<string, string>> requestHeaders, TransferResult result) =>
        result is { IsSuccess: true, Report: { } report }
        && HttpHeaderValues.FindFirst(report.ResponseHeaders, "Vary") is null
        && Storability.IsStorable("GET", report.ResponseCode, requestHeaders, report.ResponseHeaders);

    private async Task<TransferResult> FetchAsync(CacheLookup lookup, ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        using MemoryStream body = new();
        using MemoryStream headers = new();
        using MemoryStream dumpedHeaders = new();
        var requestTime = context.TimeProvider.GetUtcNow();
        var result = await next(ConditionalOn(lookup.Stored, context).WithOutputs(body, headers, context.DumpHeaderOutput is null ? null : dumpedHeaders));
        var received = new ReceivedResponse(result, headers.ToArray(), body.ToArray(), requestTime, context.TimeProvider.GetUtcNow());
        if (lookup.Stored is not null && SelectsStored(lookup.Stored, result))
        {
            return await AnswerWithUpdatedAsync(lookup, received, context);
        }

        await StoredResponseOutput.WriteIfGivenAsync(context.HeaderOutput, received.Headers, context.CancellationToken);
        await StoredResponseOutput.WriteIfGivenAsync(context.DumpHeaderOutput, dumpedHeaders.ToArray(), context.CancellationToken);
        await context.Output.WriteAsync(received.Body, context.CancellationToken);
        if (IsStorable(lookup.RequestHeaders, result))
        {
            await TryStoreAsync(lookup.Key, received.ToStoredResponse(), context.CancellationToken);
        }

        return result;
    }

    private static ITransferContext ConditionalOn(StoredResponse? stored, ITransferContext context) =>
        stored is null
            ? context
            : context.WithRequestHeaders(CacheValidation.BuildConditionalHeaders(stored).Select(field => field.Key + ": " + field.Value));

    private async Task<TransferResult> AnswerWithUpdatedAsync(CacheLookup lookup, ReceivedResponse received, ITransferContext context)
    {
        var report = received.Result.Report!;
        var updated = CacheValidation.ApplyNotModified(lookup.Stored!, report.ResponseHeaders, received.RequestTime, received.ResponseTime);
        await TryStoreAsync(lookup.Key, updated, context.CancellationToken);
        var bodyLength = await StoredResponseOutput.WriteAsync(updated, context);
        return received.Result with { BytesTransferred = bodyLength, Report = report with { ResponseCode = updated.StatusCode, ResponseHeaders = updated.Headers } };
    }

    private async Task TryStoreAsync(string key, StoredResponse response, CancellationToken cancellationToken)
    {
        try
        {
            await directory.WriteAsync(key, response, cancellationToken);
        }
        catch (IOException)
        {
            // A cache that cannot be written leaves the transfer as it was.
        }
    }

    /// <summary>The request a cache lookup was for and what the directory held for it.</summary>
    private sealed record CacheLookup(string Key, IReadOnlyList<KeyValuePair<string, string>> RequestHeaders, StoredResponse? Stored);

    /// <summary>The response curl's handler received, captured to be written on and perhaps stored.</summary>
    private sealed record ReceivedResponse(TransferResult Result, byte[] Headers, byte[] Body, DateTimeOffset RequestTime, DateTimeOffset ResponseTime)
    {
        public StoredResponse ToStoredResponse() => new(
            StoredResponseOutput.StatusLineIn(Headers, Result.Report!.ResponseCode),
            Result.Report.ResponseCode,
            Result.Report.ResponseHeaders,
            Body,
            RequestTime,
            ResponseTime);
    }
}
