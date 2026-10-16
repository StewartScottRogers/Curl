using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// An <see cref="IHstsStore" /> that hands every header to <paramref name="inner" /> with the time
/// of <paramref name="clock" /> in place of the transfer's, so a header's <c>max-age</c> counts from
/// the HSTS clock curl's debug build reads under <c>CURL_TIME</c> (ADR-0493, BL-2234).
/// </summary>
/// <param name="inner">The store that learns the header.</param>
/// <param name="clock">The clock whose time replaces the caller's.</param>
internal sealed class ClockOverridingHstsStore(IHstsStore inner, TimeProvider clock) : IHstsStore
{
    /// <inheritdoc />
    public bool StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now) =>
        inner.StoreFromResponse(origin, headerValue, clock.GetUtcNow());
}
