# Curl.Extension.MlDsaCertificates.UnitLibrary

The ML-DSA signed certificates extension (ADR-0999, ADR-1126), tested against FIPS 204 and RFC 9881.
Namespace `Curl.Extension.MlDsaCertificates`. It references only `Curl.Extension.Abstractions.UnitLibrary`,
`Curl.Cryptography.UnitLibrary` and the shared layers they bring.

- `MlDsaCertificatesExtension` gives the `--mldsa-verify` flag, its description and its certificate check hook
  activator, which `Curl.Extension.Composition` registers.
- `MlDsaCertificateCheckHook` runs on every chain curl accepted (ADR-1016): the server's certificate must be signed
  with ML-DSA, and every ML-DSA signature must verify (FIPS 204 algorithm 3, empty context) under the key of the next
  certificate sent, or of a last self-issued one, of the same parameter set (RFC 9881 sections 2 to 4). Any failure is
  exit 60.
- `CertificateFields` reads the fields of a DER certificate it needs with `System.Formats.Asn1`;
  `CertificateAlgorithm` is one `AlgorithmIdentifier`; `MlDsaAlgorithmIdentifiers` maps RFC 9881's object
  identifiers to `MlDsaParameterSet`.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.MlDsaCertificates.UnitTests`.
