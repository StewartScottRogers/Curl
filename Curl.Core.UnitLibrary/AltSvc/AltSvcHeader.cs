namespace Curl.Core.AltSvc;

/// <summary>
/// An <c>Alt-Svc</c> header value as <see cref="AltSvcHeaderParser" /> reads it: either
/// <c>clear</c> or the alternatives it offers, in order.
/// </summary>
/// <param name="IsClear">Whether the value is <c>clear</c>.</param>
/// <param name="Alternatives">The alternatives with a known ALPN; empty for <c>clear</c>.</param>
public sealed record AltSvcHeader(bool IsClear, IReadOnlyList<AltSvcAlternative> Alternatives);
