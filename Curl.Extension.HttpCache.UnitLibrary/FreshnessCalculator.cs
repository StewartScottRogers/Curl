namespace Curl.Extension.HttpCache;

/// <summary>
/// Computes a stored response's freshness lifetime (RFC 9111 §4.2.1, with the §4.2.2 heuristic of
/// a tenth of the time since <c>Last-Modified</c>), its current age (§4.2.3) and so whether it may
/// be used, against the clock <paramref name="timeProvider"/> gives.
/// </summary>
/// <param name="timeProvider">The clock "now" is read from.</param>
/// <param name="isSharedCache">Whether <c>s-maxage</c> and <c>proxy-revalidate</c> apply (§5.2.2.8, §5.2.2.10).</param>
public sealed class FreshnessCalculator(TimeProvider timeProvider, bool isSharedCache)
{
    /// <summary>How long <paramref name="response"/> stays fresh from its generation; zero when nothing gives it a lifetime.</summary>
    public TimeSpan FreshnessLifetime(StoredResponse response)
    {
        var directives = CacheControlDirectives.Parse(response.Headers);
        if (isSharedCache && directives.TryGetDeltaSeconds("s-maxage", out var sharedMaxAge))
        {
            return sharedMaxAge;
        }

        if (directives.TryGetDeltaSeconds("max-age", out var maxAge))
        {
            return maxAge;
        }

        var expires = HttpHeaderValues.FindFirst(response.Headers, "Expires");
        return expires is null ? HeuristicLifetime(response, directives) : ExplicitExpiryLifetime(expires, response);
    }

    /// <summary>The current age of <paramref name="response"/> now (RFC 9111 §4.2.3); an <c>Age</c> that is not delta-seconds counts as zero.</summary>
    public TimeSpan CurrentAge(StoredResponse response)
    {
        DeltaSeconds.TryParse(HttpHeaderValues.FindFirst(response.Headers, "Age"), out var ageValue);
        var apparentAge = Max(TimeSpan.Zero, response.ResponseTime - DateValue(response));
        var responseDelay = response.ResponseTime - response.RequestTime;
        var correctedInitialAge = Max(apparentAge, ageValue + responseDelay);
        var residentTime = timeProvider.GetUtcNow() - response.ResponseTime;
        return correctedInitialAge + residentTime;
    }

    /// <summary>Whether <paramref name="response"/> may be used now as it is, used only after validation, or used stale when validation fails.</summary>
    public CacheEntryUsability Evaluate(StoredResponse response)
    {
        var directives = CacheControlDirectives.Parse(response.Headers);
        if (directives.Has("no-cache"))
        {
            return CacheEntryUsability.MustValidate;
        }

        if (FreshnessLifetime(response) > CurrentAge(response))
        {
            return CacheEntryUsability.Fresh;
        }

        return directives.Has("must-revalidate") || (isSharedCache && directives.Has("proxy-revalidate"))
            ? CacheEntryUsability.MustValidate
            : CacheEntryUsability.Stale;
    }

    private static TimeSpan ExplicitExpiryLifetime(string expires, StoredResponse response) =>
        HttpDate.TryParse(expires, out var expiresAt) ? Max(TimeSpan.Zero, expiresAt - DateValue(response)) : TimeSpan.Zero;

    private static TimeSpan HeuristicLifetime(StoredResponse response, CacheControlDirectives directives)
    {
        var heuristicAllowed = ResponseStorability.IsHeuristicallyCacheable(response.StatusCode) || directives.Has("public");
        if (!heuristicAllowed || !HttpDate.TryParse(HttpHeaderValues.FindFirst(response.Headers, "Last-Modified"), out var lastModified))
        {
            return TimeSpan.Zero;
        }

        return Max(TimeSpan.Zero, (DateValue(response) - lastModified) / 10);
    }

    private static DateTimeOffset DateValue(StoredResponse response) =>
        HttpDate.TryParse(HttpHeaderValues.FindFirst(response.Headers, "Date"), out var date) ? date : response.ResponseTime;

    private static TimeSpan Max(TimeSpan first, TimeSpan second) => first > second ? first : second;
}
