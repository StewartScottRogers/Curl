# Curl.Cryptography.UnitLibrary

Hand-built cryptographic primitives the base class library lacks on at least one of
Windows, Linux and macOS: X25519, Ed25519, ChaCha20-Poly1305, MD4, RC4 and the rest of
the list in ADR-0118
(`Documentation/Planning/Decisions/ADR-0118-curl-cryptography-hand-builds-every-primitive-the-bcl-lacks-on-a-ci-platform.md`).
One implementation, used on every platform, so Curl behaves the same everywhere. A
primitive the BCL offers with every parameter curl uses on all three platforms is taken
from `System.Security.Cryptography` instead and never rebuilt here.

Namespace `Curl.Cryptography`. It holds:

- `ConstantTime` (internal): the branch-free helpers (mask from bit, select, less-than and
  equal masks, conditional swap, all-zero check) the primitives share.
- `Field25519` (internal): GF(2^255 - 19) arithmetic on 10 limbs of 26 and 25 bits in a
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
- `Field448` (internal): GF(2^448 - 2^224 - 1) arithmetic on 16 limbs of 28 bits in a
  caller's `Span<long>`; X448 uses it and Ed448 reuses it.
- `Edwards448` (internal): edwards448 points in projective coordinates - the complete
  addition of RFC 8032 section 5.2.4 (which also doubles), constant-time scalar
  multiplication over 456 bits, encoding, and section 5.2.3 decoding.
- `Scalar448` (internal): scalars modulo the group order L on `MontgomeryModulus` -
  reduction of a 114-byte SHAKE256 output, `MultiplyAdd` for S, and the `IsBelowOrder`
  canonical-S check.
- `Ed448` (public): RFC 8032 section 5.2 PureEdDSA signatures with an optional context of
  up to 255 bytes - `GeneratePrivateKey`, `ComputePublicKey`, `Sign` and `Verify` (each
  with and without a context), SHAKE256 from `Shake`. `Verify` is cofactorless and returns
  `false` for S >= L or a public key that does not decode.
- `X448` (public): RFC 7748 key agreement on Curve448 - `GeneratePrivateKey`,
  `ComputePublicKey`, and `TryComputeSharedSecret`, which returns `false` for the
  all-zero result of a low-order peer key, as `X25519` does.
- `ChaCha20` (public): RFC 8439's block function (`ComputeBlock`) and stream cipher
  (`ApplyKeyStream`), counter and nonce as parameters. A 12-byte nonce leaves RFC 8439's
  32-bit counter; an 8-byte nonce gives the original 64-bit counter OpenSSH uses.
- `Poly1305` (public): the one-time authenticator - `ComputeTag` and `Verify`, plus the
  internal `Initialize`, `Absorb` and `Finish` steps the AEAD drives.
- `AeadChaCha20Poly1305` (public, `IDisposable`): AEAD_CHACHA20_POLY1305 - `Encrypt` and
  `TryDecrypt`, which returns `false` with the plaintext zeroed on a wrong tag.
- `BlowfishPiDigits` (internal): Blowfish's initial P-array and S-boxes, the hexadecimal
  digits of pi.
- `BlowfishState` (internal): the P-array and S-boxes, the 16 rounds, and OpenBSD's two
  key expansions (`ExpandKey(key)`, the standard schedule, and `ExpandKey(data, key)`,
  bcrypt's salted one); `BcryptPbkdf` reuses it.
- `Blowfish` (public, `IDisposable`): the block cipher, keys of 1 to 56 bytes -
  `EncryptBlock`, `DecryptBlock`, and the CBC mode of SSH's `blowfish-cbc`, `EncryptCbc`
  and `DecryptCbc`. Constant-time (ADR-0400).
- `BcryptPbkdf` (public): OpenBSD's `bcrypt_pbkdf`, the KDF of encrypted
  `openssh-key-v1` keys - `DeriveKey`, plus the internal bcrypt `ComputeHash`.
  Constant-time (ADR-0400).
- `Camellia` (public, `IDisposable`): RFC 3713, keys of 16, 24 or 32 bytes - `EncryptBlock`,
  `DecryptBlock`, and the CBC mode of TLS's Camellia suites (RFC 5932), `EncryptCbc` and
  `DecryptCbc`, plus the internal F, FL and FLINV functions. Not constant-time (ADR-0145).
- `IBlockCipher` (internal): the forward direction of a 16-byte block cipher,
  `EncryptBlock`, the one operation GCM needs; `Aria` implements it.
- `Aria` (public, `IDisposable`): RFC 5794, keys of 16, 24 or 32 bytes - `EncryptBlock`
  and `DecryptBlock`, plus the internal SL1/SL2 (`Substitute`), A (`Diffuse`) and FO/FE
  (`Round`) functions. Not constant-time (ADR-0147).
- `GaloisCounterMode` (internal, `IDisposable`): NIST SP 800-38D GCM over any
  `IBlockCipher` - 12-byte nonce, 16-byte tag, `Encrypt` and `TryDecrypt`, plus the
  constant-time GF(2^128) `Multiply` of GHASH. Tested over the BCL's AES against `AesGcm`.
- `AeadAriaGcm` (public, `IDisposable`): ARIA-GCM for TLS's ARIA-GCM suites (RFC 6209) -
  `Encrypt` and `TryDecrypt`, which returns `false` with the plaintext zeroed on a wrong
  tag.
- `AesCtr` (public, `IDisposable`): NIST SP 800-38A counter mode on the BCL's AES-ECB,
  128-bit big-endian counter that wraps to zero (SSH `aes*-ctr`, RFC 4344).
  `ApplyKeyStream` keeps the counter and keystream position between calls, plus the
  internal `Increment`.
- `AesCbcCts` (public, `IDisposable`): CBC with ciphertext stealing as RFC 3962 defines
  it for Kerberos, on the BCL's AES-CBC - `Encrypt` and `Decrypt` of a message of at
  least one block; a longer one always has its last two blocks swapped (CBC-CS3).
- `AeadAesCcm` (public, `IDisposable`): RFC 3610 / NIST SP 800-38C CCM on the BCL's
  AES-ECB, for TLS's CCM and CCM_8 suites (the BCL's `AesCcm` is missing on macOS) - a
  nonce of 7 to 13 bytes, a tag of an even 4 to 16 bytes taken from the `tag` span's
  length, `Encrypt` and `TryDecrypt`, which returns `false` with the plaintext zeroed on a
  wrong tag, plus the internal `FormatBlock` that lays out B0 and the counter blocks.
  Constant-time.
- `MontgomeryModulus` (internal): arithmetic modulo an odd modulus, public or secret, on
  32-bit limbs - CIOS Montgomery multiplication with a masked final subtraction, a fixed
  4-bit window exponentiation whose table look-up reads all 16 entries, `Reduce` of any
  length, `Add`, `Subtract`, `MultiplyModulo`, `IsBelowModulus`, `Clear`, and `MinusTwo`, the
  Fermat inverse exponent p - 2. Its set-up doubles
  1 by masked additions, never dividing by the modulus (ADR-0195).
- `RsaCrtPrivateKey` (public, `IDisposable`): PKCS #1's RSASP1, m^d mod n on the key's CRT
  values - `ApplyPrivateExponent`, with and without the blinding bytes, blinded by r^e
  and r^-1 (by Fermat), and checked against e before the result is written. TLS 1.0 and
  1.1's MD5 + SHA-1 RSA signature uses it (ADR-0195). Constant-time.
- `FiniteFieldDiffieHellmanGroup` (public): p and g - `Group1`, `Group2` (RFC 2409),
  `Group14`, `Group16`, `Group18` (RFC 3526), `Ffdhe2048` to `Ffdhe8192` (RFC 7919), and
  `TryCreate` for an SSH group-exchange group, `false` for an even p, p below 2^8, or g
  outside 1 < g < p - 1. A parameter set, not a primitive, so it is a second public type
  beside the one ADR-0118 names.
- `FiniteFieldDiffieHellman` (public, `IDisposable`): one key pair - `Generate` (a 512-bit
  exponent, top bit set, or one byte less than p for a shorter p), the constructor taking
  x for known answers, `ComputePublicValue`, and `TryComputeSharedSecret`, `false` with the
  secret zeroed for a peer y outside 1 < y < p - 1. Values are exactly p's length,
  big-endian, leading zeros kept (RFC 7919 section 5.1); SSH's `mpint` and TLS 1.2's
  stripped premaster secret are the caller's to apply. Constant-time in x.
- `ILittleEndianCompressionFunction` (internal) and `LittleEndianMerkleDamgard<T>`
  (internal): the MD4-style construction MD4 and RIPEMD-160 share - 64-byte blocks of
  little-endian words, `0x80` padding and a little-endian bit length; each hash supplies
  its initial state and compression function through the interface's static members.
- `IBigEndianCompressionFunction` (internal), `Sha1`, `Sha256` and `Sha384` (internal
  structs) and `FixedBlockMerkleDamgard<T>` (internal): the SHA family's compression
  functions (FIPS 180-4; `Sha384` is the SHA-512 function from SHA-384's initial value)
  and the construction that hashes `prefix || header || data[..dataLength]` over the same
  number of blocks for every secret `dataLength` in a public range, as OpenSSL's
  `ssl3_cbc_digest_record` does. Pinned to the BCL's digests. Constant-time.
- `FixedBlockHmac` (public, static): HMAC-SHA1, -SHA256 and -SHA384 on that construction,
  `Compute(hash, key, header, data, dataLength, minimumDataLength, destination)` - the
  Lucky Thirteen countermeasure for TLS MAC-then-encrypt CBC records (BL-795). Byte for
  byte the BCL's HMAC. Constant-time in the key, the bytes and `dataLength`.
- `Md4` (public, `IDisposable`): RFC 1320 - static `HashData`, and incremental
  `AppendData` and `GetHashAndReset`. Constant-time.
- `Ripemd160` (public, `IDisposable`): RIPEMD-160 - the same three members. Constant-time.
- `HmacRipemd160` (public, `IDisposable`): RFC 2286 - static `HashData` and `Verify`
  (fixed-time comparison), and a keyed instance's `AppendData` and `GetHashAndReset`,
  which keeps the key for the next message. Constant-time in the key.
- `Rc4` (public, `IDisposable`): the RC4 stream cipher, keys of 1 to 256 bytes -
  `ApplyKeyStream`, which keeps its place in the keystream between calls, and
  `DiscardKeyStream`, with `Rfc4345DiscardLength` (1536) for SSH's `arcfour128` and
  `arcfour256`. Constant-time (ADR-0399).
- `Des` (public, `IDisposable`): FIPS 46-3 DES, an 8-byte key whose parity bits are
  ignored and every weak key accepted (the BCL's `DES` refuses them; ADR-0156) -
  `EncryptBlock` and `DecryptBlock`, plus the internal round function `Round`. NTLM's
  `LMOWFv1` and `DESL` use it. Constant-time (ADR-0396).
- `Cast128SubstitutionBoxes` (internal): RFC 2144 Appendix A's S1 to S4 (`RoundBoxes`)
  and S5 to S8 (`KeyScheduleBoxes`).
- `Cast128` (public, `IDisposable`): RFC 2144, keys of 5 to 16 bytes (12 rounds up to 10
  bytes, 16 above) - `EncryptBlock`, `DecryptBlock`, and the CBC mode of SSH's
  `cast128-cbc`, `EncryptCbc` and `DecryptCbc`, plus the internal round function
  `Round`. The key schedule is RFC 2144 section 2.4 as a row table. Constant-time (ADR-0398).
- `Uint14Division` (internal): constant-time division of a secret 32-bit value by a public
  modulus below 2^14, NTRU Prime's `uint32_divmod_uint14` and `int32_mod_uint14`.
- `SortingNetwork` (internal): djbsort's constant-time `crypto_sort_uint32`.
- `Sntrup761Ring` (internal): sntrup761's arithmetic in R/3 and R/q - multiplication, the
  divided-difference reciprocals, rounding, small and short polynomials from random
  bytes, and the core `Decrypt`.
- `Sntrup761Encoding` (internal): the mixed-radix `Encode` and `Decode` and, on them, the
  public key, rounded ciphertext and small-polynomial encodings.
- `Sntrup761` (public): Streamlined NTRU Prime round 3's sntrup761 KEM, byte for byte the
  submission's KAT and OpenSSH's `sntrup761.c` - `GenerateKeyPair`, `TryGenerateKeyPair`
  (the random bytes as a parameter; `false` with both keys zeroed when they give a g with
  no inverse mod 3), `Encapsulate` with and without the random bytes, and `Decapsulate`,
  which gives the implicit-rejection secret for a tampered ciphertext. Constant-time.
- `Sha224` (internal struct): SHA-256's compression from SHA-224's initial value, the
  digest cut to 28 bytes, for the HMAC-SHA-224 the BCL lacks.
- `DeterministicDsaNonce` (internal, `IDisposable`): RFC 6979 section 3.2's HMAC_DRBG -
  `NextCandidate` gives the candidate nonces in order, and `DigestLength` the hash's size.
- `DsaDomainParameters` (internal): p, q and g - `TryCreate` (OpenSSL's verification
  limits: q of 160, 224 or 256 bits, odd p longer than q up to 10,000 bits, 1 < g < p),
  `RaiseGenerator`, `InvertModuloSubprime` (by Fermat) and `ReduceHash`.
- `DsaSignature` (public, `IDisposable`): FIPS 186-4 DSA - the constructor takes p, q, g
  and x, `SignHash` signs with RFC 6979's deterministic nonce over SHA1 to SHA512, and the
  static `VerifyHash` returns `false` for bad parameters, y not below p, r or s outside
  [1, q - 1], or a mismatch. Signatures are r || s at q's length (ADR-0201). Constant-time
  in x and k. The static `HashData` hashes a message for either, SHA-224 included, which
  the BCL lacks (ADR-0211: TLS's `dsa_sha224`).
- `BrainpoolCurve` (public enum): `BrainpoolP256r1`, `BrainpoolP384r1`, `BrainpoolP512r1`
  (RFC 5639's r1 curves, TLS 1.3's `brainpoolP*r1tls13`).
- `BrainpoolDomainParameters` (internal): each curve's p, q, a, b, 3b and G, with p and q
  as `MontgomeryModulus` and the constants in Montgomery form - `For(curve)`,
  `InvertField`, `InvertOrder` (Fermat), `TryReadScalar` ([1, q - 1]) and `ReduceHash`.
- `BrainpoolPoint` (internal, static): projective points in Montgomery form - `Add` by
  Renes-Costello-Batina's complete formulas, `Double` by the same paper's complete
  doubling, `MultiplyScalar` (fixed 4-bit window, the table read whole),
  `MultiplyAndAddPublic` (Shamir's trick for ECDSA verification, skipping zero windows,
  public values only), `TryDecode` of an uncompressed point with the on-curve check,
  `ToAffine` and `EncodeUncompressed` (ADR-0217).
- `BrainpoolEcdh` (public, static): ECDH on those curves - `GeneratePrivateKey`,
  `ComputePublicKey` (uncompressed `0x04 || x || y`), `TryComputeSharedSecret` (the
  x-coordinate; `false`, zeroed, for a peer point of the wrong form, off the curve or at
  infinity) and the three `Get...Length` methods. Constant-time in the private key.
- `BrainpoolEcdsa` (public, `IDisposable`): ECDSA on those curves - the constructor takes
  the curve and d, `SignHash` signs with RFC 6979's nonce over SHA1 to SHA512,
  `ExportPublicKey`, and the static `VerifyHash`, `false` for a bad point, r or s outside
  [1, q - 1], a wrong length or a mismatch. Signatures are r || s (ADR-0217).
  Constant-time in d and k.
- `KeccakPermutation` (internal): Keccak-p[1600, 24] (FIPS 202 section 3.3) on 25
  64-bit lanes.
- `KeccakSponge` (internal, `IDisposable`): FIPS 202's sponge with byte-aligned pad10*1 -
  `Absorb` any number of pieces, then `Squeeze` any number (absorbing after squeezing
  throws `InvalidOperationException`), `Reset`; the rate and the domain byte (`0x06`
  SHA-3, `0x1F` SHAKE) are constructor parameters.
- `Sha3` (public, static): SHA3-224, SHA3-256, SHA3-384 and SHA3-512, `HashData224` to `HashData512`; the
  BCL's are missing on macOS (and SHA3-224 everywhere). Constant-time.
- `Shake` (public, `IDisposable`): SHAKE128 and SHAKE256 - static `HashData128` and
  `HashData256` for one output of any length, and `Create128`/`Create256` instances that
  `AppendData`, then `Read` output a piece at a time, then `Reset`. Ed448 and
  ML-DSA reuse it. Constant-time.
- `MlKemParameterSet` (public enum): `MlKem512`, `MlKem768`, `MlKem1024`.
- `MlKemParameters` (internal): k, eta1, eta2, du, dv and the key and ciphertext lengths
  of each set (FIPS 203 tables 2 and 3).
- `MlKemPolynomial` (internal): arithmetic mod q = 3329 without division (`DivideByModulus`
  by reciprocal and masked correction), `Ntt`, `InverseNtt`, `MultiplyNttsAndAdd`,
  `Compress`, `Decompress`, `Encode`, `Decode`, the encapsulation-key modulus check
  `AreAllBelowModulus`, and the samplers `SampleNtt` (rejection on public rho) and
  `SampleCenteredBinomial`.
- `MlKemPublicKeyEncryption` (internal): K-PKE's `GenerateKeys`, `Encrypt` and `Decrypt`
  (FIPS 203 section 5); matrix entries are sampled where used, never stored.
- `MlKem` (public, `IDisposable`): FIPS 203 ML-KEM - an instance holds one decapsulation
  key: `GenerateKey` (random, or from the seeds d and z for known answers),
  `ImportDecapsulationKey` (`ArgumentException` when its stored H(ek) does not match),
  `ExportEncapsulationKey`, `ExportDecapsulationKey` and `Decapsulate`, which compares the
  re-encrypted ciphertext branch-free and picks the key or J(z || c) by mask. Static
  `TryEncapsulate` (random m, or m given) returns `false` with its outputs zeroed for an
  encapsulation key failing the modulus check. `Dispose` zeroes the key. Constant-time.
- `MlDsaParameterSet` (public enum): `MlDsa44`, `MlDsa65`, `MlDsa87`.
- `MlDsaParameters` (internal): k, l, eta, tau, gamma1, gamma2, omega, the commitment
  hash length and the key and signature lengths of each set (FIPS 204 tables 1 and 2).
- `MlDsaPolynomial` (internal): arithmetic mod q = 8380417 without division (`Reduce` by
  a 64-bit reciprocal and masked correction), `Ntt`, `InverseNtt`, `NttEach`,
  `InverseNttEach`, `MultiplyNttsAndAdd`, `Power2Round`, `Decompose`, `HighBits`,
  `MakeHint`, `UseHint` (verification only), and the branch-free norm checks
  `ExceedsBound` and `LowBitsExceedBound`.
- `MlDsaEncoding` (internal): `Pack`/`Unpack` (SimpleBitPack), `PackCentered`/
  `UnpackCentered` (BitPack), `PackHint` and `TryUnpackHint`, `false` for a malformed hint.
- `MlDsaSampling` (internal): `SampleNtt` (rejection on public rho), `SampleBounded`
  (s1 and s2), `SampleMask` (y) and `SampleInBall`, which places the challenge's
  coefficients by a masked pass so no secret index becomes an address.
- `MlDsaInternalFunctions` (internal): FIPS 204 section 6 - `GenerateKeys`, `DeriveKeys`,
  `Sign` (the rejection loop, each attempt run to completion before its one branch),
  `Verify`, `ComputeMessageRepresentative` (mu of pure ML-DSA with a context) and
  `HashCommitment`; the matrix A is sampled once per call and kept.
- `MlDsa` (public, `IDisposable`): FIPS 204 pure ML-DSA with a context of up to 255 bytes -
  an instance holds one key pair: `GenerateKey` (random, or from the seed xi for known
  answers), `ImportPrivateKey` (`ArgumentException` when its tr or t0 does not match the
  public key its s1 and s2 give), `ExportPublicKey`, `ExportPrivateKey`, `SignData`
  (hedged; random rnd, or rnd given), `SignDataDeterministic` (rnd all zero), and static
  `VerifyData`, `false` for a malformed hint, z out of range or a mismatch. `Dispose`
  zeroes the private key. Constant-time apart from the rejection loop's iteration count
  and the samplers' rejection of out-of-range bytes, which FIPS 204 allows.
- `HpkeKem`, `HpkeKdf`, `HpkeAead` (public enums): the RFC 9180 suite identifiers HPKE
  supports - DHKEM(P-256 or X25519, HKDF-SHA256), HKDF-SHA256, and AES-128-GCM,
  AES-256-GCM or ChaCha20-Poly1305, the suites Encrypted Client Hello uses.
- `HpkeLabeledHkdf` (internal): RFC 9180 section 4's `LabeledExtract` and `LabeledExpand`
  on the BCL's `HKDF`.
- `HpkeDhkem` (internal): section 4.1's DHKEM - `TryEncapsulate` (skE given) and
  `TryDecapsulate`, over the hand-built `X25519` or the BCL's `ECDiffieHellman` on P-256.
  A peer key of the wrong length or form, off the curve (checked here, not by the
  platform) or giving X25519's all-zero result is `false`.
- `HpkeContext` (public, `IDisposable`): section 5.1's base-mode key schedule and 5.2's
  context - `Seal`, `TryOpen` (`false`, plaintext zeroed and sequence number kept, on a
  bad tag), `Export` and `SequenceNumber`. AES-GCM from the BCL's `AesGcm`,
  ChaCha20-Poly1305 from `AeadChaCha20Poly1305`. `Dispose` zeroes every secret.
- `Hpke` (public, static): HPKE base mode - `TrySetupBaseSender` (random skE, or skE given
  for known answers), `TrySetupBaseRecipient` and `GetEncapsulatedKeySize`. Constant-time
  as far as X25519 and the platform's P-256 and AES-GCM are.

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
  whether it is constant-time. Camellia (ADR-0393), ARIA (ADR-0395), DES (ADR-0396),
  CAST-128 (ADR-0398), RC4 (ADR-0399) and Blowfish (ADR-0400) read their tables by masked
  scan and are.
- **Zeroing.** Every secret, `stackalloc` temporaries and intermediate key material
  included, is cleared with `CryptographicOperations.ZeroMemory` in a `finally` block or
  in `Dispose`. Keyed types copy the key in their constructor, implement `IDisposable`,
  and throw `ObjectDisposedException` after `Dispose`.
- **Pinned by published vectors.** Every primitive is tested in
  `Curl.Cryptography.UnitTests` against its specification's published test vectors,
  with the source cited beside each vector, plus negative cases (a flipped bit, an
  invalid point, a non-canonical encoding). Tests are platform-neutral; a vector that
  takes more than a second is `TestCategory=Integration`.
- **Optimized in every configuration (ADR-0426).** The project file sets
  `<Optimize>true</Optimize>`, so the masked table scans run at Release speed under the
  Debug tests; one bcrypt hash takes tens of milliseconds, not hundreds. Keep it.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
