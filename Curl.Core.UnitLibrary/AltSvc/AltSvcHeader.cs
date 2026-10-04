using Curl.Protocol.Abstractions;

namespace Curl.Core.AltSvc;

/// <summary>
/// An <c>Alt-Svc</c> header value as <see cref="AltSvcHeaderParser" /> reads it: either
/// <c>clear</c> or the alternatives it offers, in order, and why reading stopped early.
/// </summary>
/// <param name="IsClear">Whether the value is <c>clear</c>.</param>
/// <param name="Alternatives">The alternatives with a known ALPN; empty for <c>clear</c>.</param>
/// <param name="SkipReason">
/// Why reading stopped at an alternative after <paramref name="Alternatives" /> - a bad host,
/// IPv6 literal or port, each a curl 8.21.0 <c>-v</c> line (ADR-0409) - or <see langword="null" />
/// when it read to the end or stopped where curl writes no line.
/// </param>
public sealed record AltSvcHeader(bool IsClear, IReadOnlyList<AltSvcAlternative> Alternatives, AltSvcSkipReason? SkipReason = null);
