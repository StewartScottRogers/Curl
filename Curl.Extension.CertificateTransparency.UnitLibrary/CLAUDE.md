# Curl.Extension.CertificateTransparency.UnitLibrary

The Certificate Transparency extension (ADR-0999, ADR-1119), tested against RFC 6962 and RFC 9162. Namespace
`Curl.Extension.CertificateTransparency`. It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared
layers it brings.

- `CertificateTransparencyExtension` gives the `--require-sct <log list>` option, its description and its certificate
  check hook activator, which `Curl.Extension.Composition` registers.
- `CertificateTransparencyCheckHook` reads the log list and refuses a chain curl accepted (exit 60) unless the server's
  certificate carries a valid SCT.
- `CertificateTransparencyLogList` reads the logs' keys from a version 3 JSON log list, by log ID.
- `SignedCertificateTimestampVerifier` decides whether at least one embedded SCT is valid: a listed log, a timestamp no
  later than now, and a signature that verifies.
- `EmbeddedTimestamps` reads the SCT list extension from a certificate and rebuilds its `TBSCertificate` without it.
- `SignedCertificateTimestamp` reads version 1 SCTs and lays out the data a log signed; `TlsReader` reads their TLS
  encoding.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.CertificateTransparency.UnitTests`.
