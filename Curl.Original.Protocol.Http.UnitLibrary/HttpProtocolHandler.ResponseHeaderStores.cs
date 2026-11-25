using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

// The part of HttpProtocolHandler that stores what response headers say in the cookie, Alt-Svc and HSTS stores.
public sealed partial class HttpProtocolHandler
{
    /// <summary>
    /// Asks the cookie store for the <c>Cookie</c> value to send to the transfer's URL, or
    /// gives <see langword="null" /> when cookies are off: the stored cookies alone when
    /// <see cref="HttpRequestOptions.SendsCookieStrings" /> is off (BL-1846). The store reports a
    /// limit that cut the value short to the transfer's events, before the request's header
    /// lines, as curl does.
    /// </summary>
    private string? CookieHeaderFor(ITransferContext context, HttpRequestOptions options) =>
        options.SendsCookieStrings
            ? CookieStore?.GetCookieHeader(CookieUrlOf(context.Url, options), TargetOf(context.Url).UseTls, context.TimeProvider.GetUtcNow(), context.Events)
            : CookieStore?.GetStoredCookieHeader(CookieUrlOf(context.Url, options), TargetOf(context.Url).UseTls, context.TimeProvider.GetUtcNow(), context.Events);

    /// <summary>
    /// Gives the URL cookies are matched and stored against: <paramref name="url" /> with the
    /// host of a <c>-H</c> <c>Host</c> value in its place, as curl takes its cookie host from a
    /// custom <c>Host</c> header, or <paramref name="url" /> itself when there is none.
    /// </summary>
    internal static CurlUrl CookieUrlOf(CurlUrl url, HttpRequestOptions options) =>
        HttpRequestHeadFormatter.CustomHostOf(options) is { } host
        && CurlUrl.TryParse($"{url.Scheme}://{host}{url.AbsolutePath}", false, out CurlUrl? cookieUrl)
            ? cookieUrl
            : url;

    /// <summary>
    /// Hands <paramref name="header" />, when it is a <c>Set-Cookie</c> header and cookies are
    /// on, to the cookie store with the transfer's events for its <c>-v</c> lines, as it arrives
    /// in any head of the response, 1xx heads' included (measured, BL-468 Notes).
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <param name="options">The request's options, whose <c>-H</c> <c>Host</c> value gives the cookie host (<see cref="CookieUrlOf" />).</param>
    /// <param name="header">A whole header of the response.</param>
    /// <param name="storedFromResponse">How many cookies the store has stored from this request's responses.</param>
    /// <returns>The count the store gives back, or <paramref name="storedFromResponse" /> when the store is not asked.</returns>
    private int StoreCookie(ITransferContext context, HttpRequestOptions options, HttpResponseHeader header, int storedFromResponse) =>
        CookieStore is { } store && string.Equals(header.Name, "Set-Cookie", StringComparison.OrdinalIgnoreCase)
            ? store.StoreFromResponse(CookieUrlOf(context.Url, options), header.Value, storedFromResponse, context.TimeProvider.GetUtcNow(), context.Events)
            : storedFromResponse;

    /// <summary>
    /// Hands <paramref name="header" />, when it is an <c>Alt-Svc</c> header and
    /// <paramref name="store" /> is set, to the store with the transfer's
    /// URL as the origin, and reports curl 8.21.0's <c>Added alt-svc: &lt;host&gt;:&lt;port&gt; over
    /// &lt;id&gt;</c> for each alternative it added, before the header line (measured, BL-623
    /// Notes), and for each it skipped the <c>lib/altsvc.c</c> line its reason names, in header
    /// order (measured, ADR-0409). The store decides whether a plain <c>http</c> origin's header is
    /// learned: curl's release build learns none, its debug build does under <c>CURL_ALTSVC_HTTP</c>
    /// (ADR-0497). The store is told
    /// <paramref name="responseVersion" />, the version the response came over, as curl 8.21.0
    /// passes <c>k->httpversion</c> to <c>Curl_altsvc_parse</c> (BL-947).
    /// </summary>
    private static void StoreAltSvc(ITransferContext context, IAltSvcStore? store, Version responseVersion, HttpResponseHeader header)
    {
        if (store is null
            || !string.Equals(header.Name, "Alt-Svc", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IReadOnlyList<AltSvcHeaderOutcome> outcomes = store.StoreFromResponse(context.Url, header.Value, responseVersion, context.TimeProvider.GetUtcNow());
        foreach (AltSvcHeaderOutcome outcome in outcomes)
        {
            context.Events.ReportInfo(AltSvcOutcomeLine(outcome));
        }
    }

    /// <summary>
    /// Gets curl 8.21.0's <c>-v</c> line for one <c>Alt-Svc</c> outcome: <c>Added alt-svc: ...</c>
    /// for an alternative added, or the <c>lib/altsvc.c</c> text for the reason one was skipped
    /// (ADR-0409).
    /// </summary>
    private static string AltSvcOutcomeLine(AltSvcHeaderOutcome outcome) =>
        outcome switch
        {
            { Added: { } added } => $"Added alt-svc: {added.Host}:{added.Port} over {added.Alpn}",
            { SkipReason: AltSvcSkipReason.BadHostname } => "Bad alt-svc hostname, ignoring.",
            { SkipReason: AltSvcSkipReason.BadIpv6Hostname } => "Bad alt-svc IPv6 hostname, ignoring.",
            _ => "Unknown alt-svc port number, ignoring.",
        };

    /// <summary>
    /// Hands <paramref name="header" />, when it is a <c>Strict-Transport-Security</c> header and
    /// <paramref name="store" /> is set, to the store with the transfer's URL as the origin, and
    /// reports curl 8.21.0's <c>Illegal STS header skipped</c> before the header line when the store
    /// refuses it (measured, ADR-0409). The store decides whether an <c>http</c> origin's header is
    /// learned: curl's release build learns none, its debug build does under <c>CURL_HSTS_HTTP</c>
    /// (ADR-0494).
    /// </summary>
    private static void StoreHsts(ITransferContext context, IHstsStore? store, HttpResponseHeader header)
    {
        if (store is not null
            && string.Equals(header.Name, "Strict-Transport-Security", StringComparison.OrdinalIgnoreCase)
            && !store.StoreFromResponse(context.Url, header.Value, context.TimeProvider.GetUtcNow()))
        {
            context.Events.ReportInfo("Illegal STS header skipped");
        }
    }
}
