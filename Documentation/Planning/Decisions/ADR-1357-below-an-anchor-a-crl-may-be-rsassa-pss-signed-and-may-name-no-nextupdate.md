# ADR-1357: Below an anchor a CRL may be RSASSA-PSS signed and may name no nextUpdate

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5878). Replaces ADR-1294's
"a list naming no `nextUpdate` fails closed".

## Context
Below a non-self-signed `--cacert` anchor (ADR-1294) two lists left the status "unknown", which
`--ssl-revoke-best-effort` tolerates, so a revoked certificate was accepted (fitness finding
STD-197): one signed with RSASSA-PSS, and one without the `nextUpdate` field RFC 5280 makes
optional. Measured with real certificates and lists served on loopback, curl 8.22.0 Schannel
answers "revoked" (exit 60) for a certificate either list names and accepts one neither names, with
and without best effort.

## Decision
- `CertificateRevocationList.IsSignedBy` verifies RSASSA-PSS. `RsaPssSignatureParameters` admits
  SHA-1, SHA-256, SHA-384 or SHA-512, MGF1 over the same hash, a salt as long as the hash and
  trailer field 1: the shape the base class library's `RSASignaturePadding.Pss` verifies. Other
  parameters fail the signature, so the status stays unknown. The OpenSSL build's `--crlfile` check
  shares the method, so it verifies these lists too.
- Below an anchor a list, base or delta, that names no `nextUpdate` is current
  (`IsUnexpiredAt`), as Windows treats it. `--crlfile` already accepted such a list.
