# ADR-1294: Revocation below an intermediate-only --cacert anchor is read from the CRL

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5384).
ADR-1364 (BL-5878) replaces "a list naming no `nextUpdate` fails closed": such a list is current.

## Context
With `--proxy-cacert` (or `--cacert`) holding only an issuing intermediate and revocation checking on, the
Windows chain engine reports `RevocationStatusUnknown` and `OfflineRevocation` on every element, a revoked
leaf included, even with a valid CRL reachable (measured with real certificates and a CRL served on
loopback). The BL-5317 tolerance therefore refused the chain as incomplete, and a revoked leaf got the
"chain is incomplete" text instead of "has been revoked". curl applies no `--ssl-no-revoke` to the proxy
(the proxy options never carry it), so the proxy chain is always checked.

## Decision
When the only faults are a missing root and unavailable revocation on a chain ending at a listed,
non-self-signed anchor, `ServerCertificateVerification` reads the CRLs itself for each certificate below
the anchor: it takes the http addresses from the certificate's CRL distribution points, downloads the list
(`RevocationListDownload`, behind the `FetchRevocationList` seam), and accepts it when it decodes, names the
certificate's issuer, is signed by that issuer's key and has a `nextUpdate` not yet passed. Every
certificate listed in a usable CRL means "revoked" (exit 60, `SchannelChainRevoked`); every certificate with
no usable list means "unknown" (still refused, as before); otherwise the chain is accepted. A list naming no
`nextUpdate` fails closed. The check never runs when the engine reports no unknown status, so
`--ssl-no-revoke` and the OpenSSL build are unchanged. Only http distribution points are fetched.
