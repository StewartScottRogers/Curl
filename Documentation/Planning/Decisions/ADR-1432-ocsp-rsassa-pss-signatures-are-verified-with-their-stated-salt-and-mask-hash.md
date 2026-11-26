# ADR-1432: OCSP RSASSA-PSS signatures are verified with their stated salt and mask hash

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6010; fitness finding STD-217)

## Context

`OcspSignatureAlgorithm` read only the hash from `RSASSA-PSS-params` and verified with the base
class library's PSS padding, which takes a salt as long as the hash and MGF1 over that hash. A
responder signing with any other salt had every answer refused, "revoked" included, so under
`--ssl-revoke-best-effort` Curl on Windows accepted a revoked certificate. The fitness review
measured curl 8.22.0 Schannel: it honours a salt of 64 or 0. ADR-1376 already reads all the
parameters for revocation list signatures.

## Decision

An OCSP response's or responder certificate's RSASSA-PSS signature is verified by
`Curl.Cryptography`'s `RsaPssVerification` with the hash, MGF1 hash and salt length its
parameters state, on every platform, read as ADR-1376 reads a revocation list's:

- Absent fields take RFC 4055's defaults: SHA-1, MGF1 over SHA-1, a 20-byte salt, trailer 1.
  Parameters naming SHA-256 alone therefore mean MGF1 over SHA-1 and a 20-byte salt.
- A hash or MGF1 hash other than SHA-1, SHA-256, SHA-384 or SHA-512, another mask function, a
  salt length below 0 or above `int.MaxValue`, or another trailer field is an unknown
  algorithm, so the signature is not accepted.

TLS handshake signature schemes are unchanged: their salt is as long as their hash.
