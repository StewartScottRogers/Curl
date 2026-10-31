namespace Curl.Extension.MlDsaCertificates;

/// <summary>An X.509 <c>AlgorithmIdentifier</c> (RFC 5280 section 4.1.1.2): its object identifier and whether it carries parameters.</summary>
/// <param name="Oid">The algorithm's object identifier, in dotted form.</param>
/// <param name="HasParameters">Whether the identifier carries a parameters field, even a NULL one.</param>
internal sealed record CertificateAlgorithm(string Oid, bool HasParameters);
