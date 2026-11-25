# ADR-1383: The OpenSSL build answers 53 for a CN holding a NUL and reads every subject CN (BL-5921)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1354.

## Context
ADR-1354 left two cases open: a CN with a NUL inside it was reported as a permitted subtree violation (47), and a CN in a multi-valued relative name was not checked against name constraints. Measured with curl 8.18.0 and OpenSSL 3.5.5 (WSL Ubuntu, `openssl s_server`, a leaf with no subjectAltName under a constrained intermediate given as `--cacert`; the same with the root as `--cacert`):

| Subject | Answer, exit 60 | `%{ssl_verify_result}` |
|---|---|---|
| last CN holds a NUL, inner or trailing, constrained or not | `SSL: illegal cert name field` | 1 |
| an earlier CN holds an inner NUL, last CN is the host | `unsupported or invalid name syntax (53)`, under a DNS or a directoryName constraint | 53 |
| `CN=www.example.com+OU=Unit`, excluded `example.com` | `excluded subtree violation (48)` | 48 |
| the same, permitted `other.org` | `permitted subtree violation (47)` | 47 |
| CNs `a.other.org`, `www.example.com`, permitted `other.org` | `permitted subtree violation (47)` | 47 |

curl's `ossl_verifyhost` runs before the verify result is read, so it answers first when the CN it matches (the last) holds the NUL.

## Decision
- The OpenSSL build's name check refuses a certificate with no DNS and no IP subjectAltName whose last CN holds a NUL, as `SSL: illegal cert name field`.
- `CertificateNameConstraints` checks, for the OpenSSL build and after every other name, each subject CN in order, multi-valued relative names included, and stops at the first refused: an inner NUL gives `UnsupportedNameSyntax` (53), a host-name-like CN is checked as a DNS name (ADR-1354).
- .NET's one flag for OpenSSL's unsupported-constraint errors, `HasNotSupportedNameConstraint`, is reported as 53. For a chain .NET's own engine refused with 51 or 52, 53 is not exact, but the answer before was a trust error, which is further from curl's.
- The Schannel build's answers do not change.

## Consequences
A leaf whose only CN holds a NUL never shows 53: curl answers it from the name check. A subject emailAddress in a multi-valued relative name is still not read by the name constraint check.
