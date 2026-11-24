# ADR-1345: --crlfile with a non-self-signed anchor is "unable to get issuer certificate (2)"

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation (BL-5830, STD-180).

## Context

In the OpenSSL build with `--crlfile`, a chain that ends at an anchor which is not
self-signed (an intermediate-only `--cacert` or `--capath`) fails in curl 8.18.0 with
`unable to get issuer certificate (2)` before any list is read. Curl said `certificate
revoked (23)`. A `--crlfile` that cannot be loaded (exit 82) also leaves
`%{ssl_verify_result}` at 1 in curl, where Curl left 0.

## Decision

`CertificateRevocationListFile.FirstRefusal` returns verify result 2 when the chain's last
certificate is not self-signed. Both TLS providers report verify result 1 when
`--crlfile` is unusable (exit 82) in the OpenSSL build. The finding's measurements
(REF2, curl 8.18.0 OpenSSL) are the source; the Schannel build ignores `--crlfile`.
