# ADR-0118 — `Curl.Cryptography.UnitLibrary` hand-builds every primitive the BCL lacks on any CI platform

- **Status:** Accepted. Superseded in part by ADR-0421 (slow vectors are LongRunning, not Integration).
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-669.

## Context

Curl is a complete reimplementation of curl (Stewart, 2026-09-28): SSH, the hand-built
TLS client, QUIC and HTTP/3, NTLM and Kerberos are all built in C# on the base class
library, and nothing is left out because the BCL has no primitive for it. Each missing
primitive is written by hand in its own library with its own tests, held to the same
quality gates, never taken from a package.

`System.Security.Cryptography` delegates to the operating system (CNG on Windows,
OpenSSL on Linux, Apple's frameworks on macOS), so what it offers differs by platform.
Microsoft Learn, "Cross-platform cryptography in .NET" (page dated 2026-08-03, checked
2026-09-28), states for .NET 10:

- AES-ECB, AES-CBC, AES-CFB, 3DES, DES, MD5, SHA-1, SHA-2, their HMACs, RSA, ECDSA and
  ECDH on NIST P-256, P-384 and P-521 work on Windows, Linux and macOS.
- `AesGcm` works on all three; on Apple platforms only with 16-byte tags.
- `AesCcm` works on Windows and Linux but **not on macOS**: .NET 10 removed OpenSSL
  support on macOS.
- `ChaCha20Poly1305` needs Windows 10 build 20142+, OpenSSL 1.1.0+, and works on macOS;
  it exposes neither raw ChaCha20 nor Poly1305.
- `DSA` on macOS cannot create keys, loads keys over 1024 bits with undefined behaviour,
  and has no FIPS 186-3 (SHA-2) signatures.
- Brainpool named curves: Windows 10+, some Linux distributions, **not macOS**.
- SHA-3 and SHAKE: Windows 11 build 25324+, OpenSSL 1.1.1+, **not macOS**.
- ML-KEM and ML-DSA: Windows Insider builds and OpenSSL 3.5+ only, **not macOS**.
- No Curve25519, Curve448, Ed25519, Ed448, MD4, RIPEMD-160, RC4, Blowfish, CAST-128,
  bcrypt-pbkdf, HPKE, AES-CTR, AES-CBC-CTS or integer Diffie-Hellman on any platform.
  `BigInteger.ModPow` exists but branches on the exponent's bits.

What curl needs comes from its backends. SSH: libssh2 1.11.1, the SSH library of
curl.se's official Windows build of curl 8.22.0 (https://curl.se/windows/,
https://libssh2.org/, checked 2026-09-28), offers `curve25519-sha256`, the
`diffie-hellman-group*` methods, `ssh-ed25519`, `ssh-dss`, `aes*-ctr`, `blowfish-cbc`,
`cast128-cbc`, `arcfour`, `arcfour128` and `hmac-ripemd160`; libssh adds
`chacha20-poly1305@openssh.com`; encrypted `openssh-key-v1` keys use bcrypt-pbkdf and
`aes256-ctr`. TLS on Linux and macOS: OpenSSL 3.5.7 (measured 2026-09-28 with
`openssl list -tls-groups` and `openssl ciphers -s -v DEFAULT`) lists the groups
`secp256r1`, `secp384r1`, `secp521r1`, `x25519`, `x448`, `brainpoolP256r1tls13`,
`brainpoolP384r1tls13`, `brainpoolP512r1tls13`, `ffdhe2048` to `ffdhe8192`, `MLKEM512`,
`MLKEM768`, `MLKEM1024`, `SecP256r1MLKEM768`, `X25519MLKEM768` and `SecP384r1MLKEM1024`,
and its default cipher list enables `TLS_CHACHA20_POLY1305_SHA256` and `DHE-RSA-*`
suites; CCM suites are available by name. QUIC header protection with ChaCha20 needs raw
ChaCha20 (RFC 9001 section 5.4.4). NTLM needs MD4 and RC4; Kerberos needs MD4, RC4 and
AES-CBC-CTS; Encrypted Client Hello needs HPKE.

## Decision

### The rule

A primitive curl needs is taken from `System.Security.Cryptography` only when the BCL
provides it, with every parameter curl uses, on **all three** CI platforms (Windows,
Linux, macOS). Otherwise it is hand-built once in `Curl.Cryptography.UnitLibrary` and
that one implementation is used on every platform, so Curl behaves identically
everywhere and a test that passes on Windows means the same thing on macOS. Nothing is
omitted because the BCL lacks it.

### Hand-built primitives

"Missing" means the BCL has no such type; "partial" says where it is missing.

| Primitive (public type) | Specification | Consumers | BCL: Windows / Linux / macOS | Built by | Test vectors |
| --- | --- | --- | --- | --- | --- |
| X25519 (`X25519`) | RFC 7748 | SSH `curve25519-sha256` (BL-678); TLS 1.3 and QUIC key share `x25519`, `X25519MLKEM768` (BL-699); HPKE (BL-677) | missing / missing / missing | BL-671 | RFC 7748 §5.2, §6.1 |
| Ed25519 (`Ed25519`) | RFC 8032 §5.1 | SSH `ssh-ed25519` host and user keys (BL-678, BL-681); TLS `ed25519` (BL-699) | missing / missing / missing | BL-672 | RFC 8032 §7.1 |
| ChaCha20 (`ChaCha20`), Poly1305 (`Poly1305`), the AEAD (`AeadChaCha20Poly1305`) | RFC 8439; OpenSSH `PROTOCOL.chacha20poly1305` | SSH `chacha20-poly1305@openssh.com` (BL-679); QUIC header protection (BL-723); TLS `TLS_CHACHA20_POLY1305_SHA256` and `*-CHACHA20-POLY1305` (BL-699, BL-702); HPKE (BL-677) | AEAD only, Windows 10 20142+ / OpenSSL 1.1.0+ / yes; raw pieces missing everywhere | BL-673 | RFC 8439 §2 and Appendix A.1 to A.5 |
| Blowfish (`Blowfish`), bcrypt-pbkdf (`BcryptPbkdf`) | Schneier 1993; OpenBSD `bcrypt_pbkdf.c` | SSH `blowfish-cbc` (BL-680); encrypted `openssh-key-v1` keys (BL-681) | missing / missing / missing | BL-674 | Schneier's vectors; OpenBSD `regress/lib/libutil/bcrypt_pbkdf` |
| MD4 (`Md4`) | RFC 1320 | NTLM `NTOWFv1` (BL-684); Kerberos `rc4-hmac` string-to-key (BL-686) | missing / missing / missing | BL-675 | RFC 1320 A.5 |
| RIPEMD-160 (`Ripemd160`), HMAC-RIPEMD-160 (`HmacRipemd160`) | Dobbertin, Bosselaers, Preneel 1996; RFC 2286 | SSH `hmac-ripemd160`, `hmac-ripemd160@openssh.com` (BL-680) | missing / missing / missing | BL-675 | Bosselaers' page; RFC 2286 §2 |
| RC4 (`Rc4`) | RFC 6229 (vectors), RFC 4345 (discard) | SSH `arcfour`, `arcfour128` (BL-680); Kerberos `rc4-hmac` (BL-686); NTLM key exchange (BL-684) | missing / missing / missing | BL-676 | RFC 6229 |
| DES (`Des`) | FIPS 46-3 | NTLM `LMOWFv1`, `DESL` and the `NEGOTIATE_LM_KEY` key exchange key (BL-684, ADR-0156) | **partial** everywhere: the BCL's `DES` throws on the weak keys an empty password's LM hash needs | BL-684 | NIST SP 500-20; the BCL's `DES` for the keys it accepts |
| CAST-128 (`Cast128`) | RFC 2144 | SSH `cast128-cbc` (BL-680) | missing / missing / missing | BL-676 | RFC 2144 Appendix B |
| HPKE base mode (`Hpke`) | RFC 9180 | Encrypted Client Hello, `--ech` (BL-706) | missing / missing / missing | BL-677 | RFC 9180 Appendix A (base mode) |
| AES-CTR (`AesCtr`), AES-CBC-CTS (`AesCbcCts`) | NIST SP 800-38A; RFC 3962 | SSH `aes*-ctr` (BL-565) and `openssh-key-v1` `aes256-ctr` (BL-681); Kerberos `aes*-cts-*` (BL-686) | missing / missing / missing (built on the BCL's AES-ECB and AES-CBC) | BL-737 | SP 800-38A F.5; RFC 3962 Appendix B |
| AES-CCM (`AeadAesCcm`) | RFC 3610; NIST SP 800-38C | TLS `TLS_AES_128_CCM_SHA256`, `TLS_AES_128_CCM_8_SHA256`, TLS 1.2 CCM suites (BL-700, BL-702) | yes / yes / **no** (.NET 10) | BL-738 | RFC 3610 §8; SP 800-38C Appendix C |
| Finite-field Diffie-Hellman (`FiniteFieldDiffieHellman`) | RFC 2409, RFC 3526, RFC 4419, RFC 7919 | SSH `diffie-hellman-group*` and `group-exchange` (BL-678); TLS `DHE-*` suites and `ffdhe*` groups (BL-703); TLS-SRP (BL-704); DSA (BL-745) | missing / missing / missing (`BigInteger.ModPow` is not constant-time) | BL-739 | RFC group primes; NIST CAVP KAS FFC; cross-check with `BigInteger.ModPow` on public test exponents |
| X448 (`X448`) | RFC 7748 | TLS 1.3 key share `x448` (BL-699, BL-709) | missing / missing / missing | BL-740 | RFC 7748 §5.2, §6.2 |
| Ed448 (`Ed448`) | RFC 8032 §5.2 | TLS `ed448` (BL-699, BL-709); Ed448 client certificates | missing / missing / missing | BL-741 | RFC 8032 §7.4 |
| Brainpool ECDH and ECDSA (`BrainpoolEcdh`, `BrainpoolEcdsa`) | RFC 5639, RFC 8734, RFC 6979 | TLS 1.3 `brainpoolP*r1tls13` groups and `ecdsa_brainpoolP*r1tls13_*` schemes (BL-699, BL-709) | Windows 10+ / some distributions / **no** | BL-742 | RFC 7027 Appendix A; Wycheproof brainpool ECDSA |
| SHA-3 and SHAKE (`Sha3`, `Shake`) | FIPS 202 | ML-KEM (BL-743), ML-DSA (BL-744), Ed448 (BL-741) | Windows 11 25324+ / OpenSSL 1.1.1+ / **no** | BL-743 | NIST SHA-3 examples; CAVP byte-oriented vectors |
| ML-KEM (`MlKem`) | FIPS 203 | TLS 1.3 `X25519MLKEM768`, `SecP256r1MLKEM768`, `SecP384r1MLKEM1024`, `MLKEM*` groups (BL-699, BL-709) | Insider builds / OpenSSL 3.5+ / **no** | BL-743 | NIST ACVP ML-KEM |
| ML-DSA (`MlDsa`) | FIPS 204 | TLS 1.3 `mldsa44`, `mldsa65`, `mldsa87` (BL-699, BL-709) | Insider builds / OpenSSL 3.5+ / **no** | BL-744 | NIST ACVP ML-DSA |
| DSA (`DsaSignature`) | FIPS 186-4, RFC 6979 | SSH `ssh-dss` (ADR-0122, BL-564); TLS 1.2 `dsa_*` signatures (BL-703, BL-709) | yes / yes / **partial** (no key creation, no FIPS 186-3) | BL-745 | NIST CAVP FIPS 186-4 `SigVer`; RFC 6979 A.2 |
| sntrup761 (`Sntrup761`) | NTRU Prime round 3 (2020), OpenSSH `sntrup761.c` | SSH `sntrup761x25519-sha512`, `sntrup761x25519-sha512@openssh.com` (ADR-0122, BL-748) | missing / missing / missing | BL-747 | Round-3 submission KAT (`kat_kem.rsp`) |

BL-737 to BL-745 are filed by this decision; BL-671 to BL-677 were filed with it; the
sntrup761 row and BL-747 were added by ADR-0122.

### Taken from the BCL

Consumers use these directly, on every platform, with no hand-built fallback:
`Aes` (ECB, CBC, CFB), `AesGcm` (every consumer, TLS, QUIC, SSH `aes*-gcm@openssh.com`
and HPKE, uses 16-byte tags, which all three platforms support), `TripleDES` (not `DES`,
whose weak-key refusal ADR-0156 works around), `MD5`, `SHA1`, `SHA256`, `SHA384`, `SHA512`, their `HMAC*` types, `HKDF`,
`Rfc2898DeriveBytes.Pbkdf2`, `RSA` (PKCS #1 v1.5, PSS, OAEP), `ECDsa` and
`ECDiffieHellman` on NIST P-256, P-384 and P-521, `RandomNumberGenerator`, and
`CryptographicOperations`. The BCL's `ChaCha20Poly1305`, `AesCcm` and `DSA` are not
used: their hand-built counterparts above replace them everywhere. SHA-512/256 for
HTTP Digest stays where it already lives, `Curl.Authentication.UnitLibrary`'s
`Sha512Slash256`, until a second library needs it; then it moves here.

### API shape

- Namespace `Curl.Cryptography`, matching the project folder. The library references
  the BCL only, is AOT-compatible, and never opens a socket or a file: it takes and
  returns bytes. Which libraries may reference it, and what it may reference, is ADR-0120.
- One public type per primitive, named as its specification names it (the table's
  "public type" column). Where that name equals a `System.Security.Cryptography` type,
  the specification's own AEAD or scheme identifier prefixes it (`AeadAesCcm`,
  `AeadChaCha20Poly1305`, `DsaSignature`) so both namespaces can be imported together.
  Field elements, curve points and other internals are `internal`.
- Span-based: inputs are `ReadOnlySpan<byte>`, outputs are written to a caller's
  `Span<byte>`; no method allocates an array for its result.
- Stateless operations (hashes' one-shot form, X25519, signature verification, HPKE
  single-shot seal) are `static` methods. Keyed or stateful primitives (a stream
  cipher's keystream position, an incremental hash, a key pair) are `sealed` classes
  that copy the key in their constructor and implement `IDisposable`; `Dispose` zeroes
  every secret they hold, and use after `Dispose` throws `ObjectDisposedException`.
- A wrong length or other caller mistake throws `ArgumentException`. Anything a peer can
  send is never an exception: a failed tag, a bad signature, an invalid or low-order
  point, an all-zero X25519 or X448 result, or an out-of-range Diffie-Hellman value is a
  `false` return from a `Try…` or `Verify` method, with the destination buffer zeroed.
  That `false` is the typed failure the primitive tasks refer to; the protocol decides
  the curl exit code.
- Every operation that needs randomness has an overload that takes the random bytes
  (seed, ephemeral key, nonce) as a parameter, so published vectors can be reproduced;
  the convenience overload fills them from `RandomNumberGenerator`.

### Constant time and zeroing

- No branch, loop bound, array index or memory address depends on a secret: conditional
  swaps and selects are masks, field arithmetic uses fixed-width limbs, secrets never
  go through `/`, `%` or `BigInteger`.
- Tags, MACs and other secret-derived values are compared with
  `CryptographicOperations.FixedTimeEquals`.
- Every secret, including `stackalloc` temporaries and intermediate key material, is
  cleared with `CryptographicOperations.ZeroMemory` in a `finally` block or `Dispose`.
- Each hand-built type states in its XML documentation whether it is constant-time.
  Three are not and cannot be without a design their specification does not describe:
  Blowfish, CAST-128 and RC4 index key-dependent tables by design. They are built as
  specified, their XML docs say so, and they exist because curl's SSH backends offer
  them. Work on public data only (signature verification, hashing a public message)
  need not be constant-time. ML-DSA's rejection loop may reveal its iteration count,
  as FIPS 204 permits.

### Tests

Every primitive is pinned by the published vectors in the table, with the source cited
beside each vector in the test, plus negative cases (a flipped bit, an invalid point, a
non-canonical encoding). Tests are platform-neutral and run in CI on all three
platforms. Vectors that take more than a second (RFC 7748's million iterations) are
marked `TestCategory=Integration`. The library meets the solution's gates: 100% line and
branch coverage, cyclomatic complexity of at most 10, CRAP of at most 30.

### Adding a primitive

A later decision that finds another primitive the BCL lacks on any CI platform (BL-560's
SSH algorithm list, BL-695's TLS cipher-suite list) files a task for it in
`Curl.Cryptography.UnitLibrary` under these same rules and says in its own ADR that it
amends this list. It is never left out.

## Consequences

- Curl's cryptography behaves the same on Windows, Linux and macOS, including macOS,
  where .NET 10 lost AES-CCM and never had SHA-3, brainpool, ML-KEM or full DSA.
- Every protocol task knows where its primitive comes from; BL-565, BL-681 and BL-686
  now depend on BL-737 instead of building CTR or CTS themselves.
- Hand-written cryptography is a security liability: it is reviewed against its
  specification, pinned by published vectors and negative cases, and held to the
  constant-time rules above, but it has not had the scrutiny of OpenSSL or CNG. Blowfish,
  CAST-128 and RC4 are known to leak key-dependent timing.
- Not using the BCL's `ChaCha20Poly1305` and `AesCcm` gives up hardware-accelerated
  paths on platforms that have them. Correctness and identical behaviour come first;
  speed can be revisited with measurements.
- Twenty-six public types in one library is a large surface; one type per primitive
  keeps each small enough for the complexity gate.

## Alternatives considered

- **Use the BCL wherever the current platform supports it, hand-built elsewhere.**
  Two implementations of each such primitive, and a macOS-only code path that Windows
  lanes never exercise. Rejected for one implementation everywhere.
- **Hand-build everything, including AES-GCM, RSA and the NIST curves.** Far more
  hand-written cryptography with no platform gap to justify it. Rejected.
- **A package (BouncyCastle, NSec, libsodium bindings).** Forbidden by the BCL-only rule,
  and native bindings break the native AOT publish. Rejected.
- **Leave out what the BCL lacks (Blowfish, CAST-128, ML-KEM, brainpool).** Contrary to
  Stewart's standing rule that a complete reimplementation leaves nothing out. Rejected.
- **Put each primitive in the protocol library that uses it.** Protocols may not
  reference each other, so shared primitives (X25519 for SSH and TLS, RC4 for SSH,
  NTLM and Kerberos) would be duplicated. Rejected.
