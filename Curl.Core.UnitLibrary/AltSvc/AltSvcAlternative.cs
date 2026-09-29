namespace Curl.Core.AltSvc;

/// <summary>
/// One alternative an <c>Alt-Svc</c> header value offers, as <see cref="AltSvcHeaderParser" />
/// reads it.
/// </summary>
/// <param name="Alpn">The HTTP version to reach it over.</param>
/// <param name="Host">Its host, or <see langword="null" /> when the value gave none, meaning the origin's.</param>
/// <param name="Port">Its port.</param>
/// <param name="MaxAgeSeconds">Its <c>ma</c>, 86400 (24 hours) when not given or unreadable.</param>
/// <param name="Persist">Whether it carried <c>persist=1</c>.</param>
public sealed record AltSvcAlternative(AltSvcAlpn Alpn, string? Host, int Port, long MaxAgeSeconds, bool Persist);
