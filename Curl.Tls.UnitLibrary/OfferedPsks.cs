namespace Curl.Tls;

/// <summary>A ClientHello's <c>pre_shared_key</c> offer (RFC 8446 section 4.2.11): the identities and one binder for each.</summary>
/// <param name="Identities">The offered identities.</param>
/// <param name="Binders">The binder HMACs, one per identity, in the same order.</param>
public sealed record OfferedPsks(IReadOnlyList<PskIdentity> Identities, IReadOnlyList<byte[]> Binders);
