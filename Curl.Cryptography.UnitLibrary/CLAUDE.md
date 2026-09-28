# Curl.Cryptography.UnitLibrary

Hand-built cryptographic primitives the base class library lacks on at least one of
Windows, Linux and macOS: X25519, Ed25519, ChaCha20-Poly1305, MD4, RC4 and the rest of
the list in ADR-0118
(`Documentation/Planning/Decisions/ADR-0118-curl-cryptography-hand-builds-every-primitive-the-bcl-lacks-on-a-ci-platform.md`).
One implementation, used on every platform, so Curl behaves the same everywhere. A
primitive the BCL offers with every parameter curl uses on all three platforms is taken
from `System.Security.Cryptography` instead and never rebuilt here.

Namespace `Curl.Cryptography`. It holds:

- `ConstantTime` (internal): the branch-free helpers (mask from bit, select, conditional
  swap, all-zero check) the primitives share.
- `Field25519` (internal): GF(2^255 - 19) arithmetic on 16 limbs of 16 bits in a
  caller's `Span<long>`; X25519 uses it and Ed25519 reuses it.
- `Edwards25519` (internal): edwards25519 points in extended coordinates - addition,
  constant-time scalar multiplication, encoding, and RFC 8032 section 5.1.3 decoding.
- `Scalar25519` (internal): scalars modulo the group order L - reduction of a SHA-512
  output, `MultiplyAdd` for S, and the `IsBelowOrder` canonical-S check.
- `Ed25519` (public): RFC 8032 section 5.1 signatures - `GeneratePrivateKey`,
  `ComputePublicKey`, `Sign` and `Verify`, which is cofactorless and returns `false` for
  S >= L or a public key that does not decode.
- `X25519` (public): RFC 7748 key agreement - `GeneratePrivateKey`, `ComputePublicKey`,
  and `TryComputeSharedSecret`, which returns `false` for the all-zero result of a
  low-order peer key.
- `ChaCha20` (public): RFC 8439's block function (`ComputeBlock`) and stream cipher
  (`ApplyKeyStream`), counter and nonce as parameters. A 12-byte nonce leaves RFC 8439's
  32-bit counter; an 8-byte nonce gives the original 64-bit counter OpenSSH uses.
- `Poly1305` (public): the one-time authenticator - `ComputeTag` and `Verify`, plus the
  internal `Initialize`, `Absorb` and `Finish` steps the AEAD drives.
- `AeadChaCha20Poly1305` (public, `IDisposable`): AEAD_CHACHA20_POLY1305 - `Encrypt` and
  `TryDecrypt`, which returns `false` with the plaintext zeroed on a wrong tag.

The remaining primitives land under their own tasks (BL-674 to BL-677, BL-737
to BL-745).

## Rules

- **Base class library only.** No package, no project reference. Bytes in, bytes out:
  never open a socket or a file.
- **API shape (ADR-0118).** One public type per primitive, named as its specification
  names it; internals stay `internal`. Inputs are `ReadOnlySpan<byte>`, outputs go to a
  caller's `Span<byte>`, and no method allocates an array for its result. A caller
  mistake (a wrong length) throws `ArgumentException`; anything a peer can send (a bad
  tag, a bad signature, an invalid or low-order point, an all-zero shared secret) is a
  `false` from a `Try...` or `Verify` method, with the destination zeroed. Every
  operation that needs randomness has an overload taking the random bytes, so published
  vectors reproduce.
- **Constant time.** No branch, loop bound, array index or memory address depends on a
  secret: select and swap with masks (`ConstantTime`), fixed-width limbs, never `/`, `%`
  or `BigInteger` on a secret. Compare tags and MACs with
  `CryptographicOperations.FixedTimeEquals`. Each public type says in its XML docs
  whether it is constant-time; Blowfish, CAST-128 and RC4 are not, by design, and say so.
- **Zeroing.** Every secret, `stackalloc` temporaries and intermediate key material
  included, is cleared with `CryptographicOperations.ZeroMemory` in a `finally` block or
  in `Dispose`. Keyed types copy the key in their constructor, implement `IDisposable`,
  and throw `ObjectDisposedException` after `Dispose`.
- **Pinned by published vectors.** Every primitive is tested in
  `Curl.Cryptography.UnitTests` against its specification's published test vectors,
  with the source cited beside each vector, plus negative cases (a flipped bit, an
  invalid point, a non-canonical encoding). Tests are platform-neutral; a vector that
  takes more than a second is `TestCategory=Integration`.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
