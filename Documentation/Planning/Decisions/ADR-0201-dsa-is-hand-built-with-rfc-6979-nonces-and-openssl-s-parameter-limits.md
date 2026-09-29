# ADR-0201 — DSA is hand-built with RFC 6979 nonces and OpenSSL's parameter limits

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-745.

## Context

ADR-0118 puts DSA in `Curl.Cryptography.UnitLibrary` because the BCL's `DSA` cannot
create keys on macOS and does not follow FIPS 186-3 above 1024 bits there. Its consumers
are SSH's `ssh-dss` host and user keys and TLS 1.2's `dsa_sha1`/`dsa_sha256` signatures
(BL-703, BL-709). Four questions had no answer in the task: where the nonce comes from,
which hashes signing supports, which domain parameters verification accepts, and what
the signature's byte layout is.

## Decision

- **`DsaSignature`** (public, `IDisposable`) holds p, q, g and x and signs a hash the
  caller computed; the static `VerifyHash(p, q, g, y, hash, signature)` verifies. Every
  value is big-endian and may carry leading zero bytes, so an SSH `mpint` goes in as it
  stands.
- **Nonce: RFC 6979 section 3.2, always.** A deterministic k needs no randomness, so a
  bad random number generator can never leak x, and published vectors (RFC 6979 A.2.1
  and A.2.2) reproduce byte for byte. OpenSSL 3.2 offers the same nonce as an option;
  a verifier cannot tell the difference, so matching curl needs nothing more.
- **Hashes: SHA-1, SHA-224, SHA-256, SHA-384 and SHA-512**, named by
  `HashAlgorithmName` (SHA-224 by `new HashAlgorithmName("SHA224")`). The HMAC of
  RFC 6979 is the BCL's for four of them; the BCL has no SHA-224 on any platform, so
  `Sha224` (SHA-256's compression from SHA-224's initial value) runs through
  `FixedBlockHmac`'s construction. Verification takes a hash of any length and uses its
  leftmost N bits (FIPS 186-4 section 4.6).
- **Parameters: what OpenSSL's verification accepts**, since the Linux curl is the one
  with the stated limits: q of 160, 224 or 256 bits, an odd p longer than q and at most
  10,000 bits (`OPENSSL_DSA_MAX_MODULUS_BITS`), and 1 < g < p. Primality of p and q and
  the order of g are not tested, as neither OpenSSL nor Schannel tests them when it
  verifies; a bad group only makes its signatures fail. Anything outside is `false` from
  `VerifyHash` (a peer sent it) and `ArgumentException` from the constructor (the
  caller's own key).
- **Signature: r || s, each exactly q's length** - SSH's `ssh-dss` blob (RFC 4253
  section 6.6) unchanged; TLS's DER `Dss-Sig-Value` is the caller's to encode.
- **Constant time.** Signing runs on `MontgomeryModulus`: g^k mod p by the fixed-window
  ladder, k^-1 as k^(q-2) mod q by the same ladder (no extended Euclid), and masked limb
  arithmetic for x * r and the sum. The only branch is RFC 6979's rejection of a
  candidate k outside [1, q - 1] or giving r or s of 0; every candidate is fully computed
  and the three conditions combined without short-circuit, so the branch shows only that
  a discarded candidate was discarded. x is zeroed on `Dispose`.

## Consequences

- `MontgomeryModulus` makes `Add` public and takes over `MinusTwo` from
  `RsaCrtPrivateKey`; `FixedBlockHmac`'s generic `Compute` is internal rather than
  private so `DeterministicDsaNonce` can run it on `Sha224`.
- Pinned by RFC 6979 A.2.1 and A.2.2 (20 signatures) and by the 60 NIST CAVP FIPS 186-4
  `SigVer` vectors for (1024, 160, SHA-1), (2048, 224, SHA-224), (2048, 256, SHA-256)
  and (3072, 256, SHA-256).
- Wiring `ssh-dss` and TLS DSA signatures to it is their own tasks' work.
