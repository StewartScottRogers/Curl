using System.Collections.Concurrent;
using System.Globalization;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.RateLimit;

/// <summary>
/// The <c>--ratelimit-pace</c> transfer hook (ADR-1100), one per option group and shared by its transfers. After
/// each HTTP response it reads the <c>RateLimit</c> field (draft-ietf-httpapi-ratelimit-headers-11 §4): a member whose
/// remaining quota <c>r</c> is 0 (§4.1.1) holds the origin for its reset <c>t</c> seconds (§4.1.2), or, with no
/// <c>t</c>, for the window <c>w</c> of the <c>RateLimit-Policy</c> member of the same name (§3.1.3); the longest hold
/// wins. Before each transfer it waits, on the transfer's clock, until the origin's hold has passed (§7, client
/// behaviour), so a <c>--retry</c> attempt or a parallel transfer to that origin is not sent into a used-up quota. A
/// response with quota left changes nothing, and a transfer that is not HTTP runs unchanged.
/// </summary>
internal sealed class RateLimitTransferHook : ITransferHook
{
    /// <summary>The longest hold, in seconds: about 49.7 days, the most <see cref="Task.Delay(TimeSpan, TimeProvider)" /> takes.</summary>
    internal const long LongestHoldSeconds = 4_294_967;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _heldUntil = new(StringComparer.Ordinal);

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        if (context.Http is null)
        {
            return await next(context);
        }

        var origin = OriginOf(context.Url);
        await WaitForHoldAsync(origin, context);
        var result = await next(context);
        RecordHold(origin, result, context.TimeProvider);
        return result;
    }

    /// <summary>Gives how long a response's fields say to hold its origin.</summary>
    /// <param name="headers">The response's header fields.</param>
    /// <returns>The longest hold of a used-up quota, or <see langword="null" /> when no quota is used up or none says for how long.</returns>
    internal static TimeSpan? HoldFor(IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        var policies = RateLimitField.Parse(FieldValueOf(headers, "RateLimit-Policy"));
        long? longest = null;
        foreach (var limit in RateLimitField.Parse(FieldValueOf(headers, "RateLimit")).Where(limit => limit.ParameterOf("r") == 0))
        {
            longest = Math.Max(longest ?? 0, SecondsToHold(limit, policies));
        }

        return longest is > 0 ? TimeSpan.FromSeconds(Math.Min(longest.Value, LongestHoldSeconds)) : null;
    }

    private static long SecondsToHold(RateLimitItem limit, IReadOnlyList<RateLimitItem> policies) =>
        limit.ParameterOf("t") ?? policies.LastOrDefault(policy => policy.Name == limit.Name)?.ParameterOf("w") ?? 0;

    private async Task WaitForHoldAsync(string origin, ITransferContext context)
    {
        if (_heldUntil.TryGetValue(origin, out var heldUntil) && heldUntil - context.TimeProvider.GetUtcNow() is { Ticks: > 0 } wait)
        {
            await Task.Delay(wait, context.TimeProvider, context.CancellationToken);
        }
    }

    private void RecordHold(string origin, TransferResult result, TimeProvider clock)
    {
        if (result.Report is { } report && HoldFor(report.ResponseHeaders) is { } hold)
        {
            var until = clock.GetUtcNow() + hold;
            _heldUntil.AddOrUpdate(origin, until, (_, held) => new DateTimeOffset(Math.Max(held.UtcTicks, until.UtcTicks), TimeSpan.Zero));
        }
    }

    private static string OriginOf(CurlUrl url) =>
        string.Create(CultureInfo.InvariantCulture, $"{url.Scheme.ToLowerInvariant()}://{url.Host.ToLowerInvariant()}:{url.Port}");

    private static string? FieldValueOf(IReadOnlyList<KeyValuePair<string, string>> headers, string name)
    {
        var values = headers.Where(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(header => header.Value).ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }
}
