# ADR-1128: The DANE TLSA matcher compares records with the chain as the server sent it

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4334
- Decided by Claude under Stewart's delegation

## Context

BL-4151's Dane extension needs to decide whether a server's certificate chain matches its TLSA records (RFC 6698
section 2.1, RFC 7671 sections 5.1 to 5.4). An extension's certificate check runs only after Curl's own PKIX
verification has accepted the chain, and can only refuse it (ADR-1016).

## Decision

- `Curl.Extension.Dane.UnitLibrary/Tlsa/TlsaCertificateMatcher.Match` takes the chain as DER, the server's own
  certificate first, and the record set, and returns a `TlsaMatchResult`: `Match`, `NoMatch`, or `NoUsableRecords`
  when every record has a usage above 3, a selector above 1 or a matching type above 2 (RFC 6698 section 4.1). An
  empty set is `NoUsableRecords` too, so the caller treats both alike.
- PKIX-EE(1) and DANE-EE(3) match only the first certificate; PKIX-TA(0) and DANE-TA(2) match any certificate after
  it, never the server's own (as OpenSSL's DANE does). A trust anchor the server did not send cannot match:
  RFC 7671 section 5.2.2 asks servers with DANE-TA records to send it.
- The matcher checks no names, expiry or signatures: RFC 7671 section 5.1 has DANE-EE ignore names and expiry, and
  the chain's signatures and, for PKIX usages, its path were already checked by Curl's PKIX verification.
- Selector 1 is `X509Certificate2.PublicKey.ExportSubjectPublicKeyInfo`, parsed only when a record asks for it.
  Association data is compared with an ordinary sequence comparison: it is public DNS data, not a secret.

## Consequences

- A DANE-TA record for a private CA works only when Curl's PKIX step accepts the chain (for example with
  `--cacert` or `--insecure`); widening that is BL-4151's decision on the hook, not the matcher's.
