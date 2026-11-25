# ADR-1376: RSASSA-PSS CRL signatures verify with any salt length they name

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5929). Replaces ADR-1364's
"a salt as long as the hash" and, for the OpenSSL build, its "MGF1 over the same hash".

## Context
ADR-1364 verified RSASSA-PSS revocation lists with the base class library's
`RSASignaturePadding.Pss`, which takes only a salt as long as the hash and MGF1 over the same
hash. `openssl ca -gencrl` signs with the longest salt unless told otherwise.

Measured 2026-11-24 with lists `openssl ca -gencrl` signed under SHA-256 and RSA 2048, each case
against a server certificate the list names and one it does not. Schannel: curl 8.22.0, below an
intermediate `--cacert` anchor, lists fetched from a loopback distribution point. OpenSSL: curl
8.18.0 on OpenSSL 3.5.5, `--cacert` root and `--crlfile`.

| List signature | Schannel, named / not named | OpenSSL, named / not named |
| --- | --- | --- |
| salt 222 (the longest), or 0 | revoked / accepted | revoked (23) / accepted |
| MGF1 over SHA-1 (salt 32), or SHA-384 (salt 20) | status unknown / status unknown | revoked (23) / accepted |
| parameters name salt 221, signed with 222 | status unknown / status unknown | CRL signature failure (8) / the same |
| last signature byte flipped | status unknown / status unknown | CRL signature failure (8) / the same |

Revoked and status unknown are exit 60 under Schannel; `--ssl-revoke-best-effort` accepts status
unknown and still refuses revoked.

## Decision
- `Curl.Cryptography.RsaPssVerification` is a hand-built RSASSA-PSS verification (RFC 8017,
  8.1.2 and 9.1.2) taking the message hash, the MGF1 hash and the exact salt length. It uses the
  public key only, so it need not be constant time. Every RSASSA-PSS list goes through it: one
  path, the same on every platform.
- `RsaPssSignatureParameters` reads SHA-1, SHA-256, SHA-384 or SHA-512 for either hash, any salt
  length from zero to `int.MaxValue`, and trailer field 1.
- `CertificateRevocationList.IsSignedBy` leaves a list whose MGF1 hash is not its message hash
  unverified, as Schannel does, unless the caller says `anotherMaskHashVerifies`, which
  `CertificateRevocationListFile` (the OpenSSL build's `--crlfile`) does.
