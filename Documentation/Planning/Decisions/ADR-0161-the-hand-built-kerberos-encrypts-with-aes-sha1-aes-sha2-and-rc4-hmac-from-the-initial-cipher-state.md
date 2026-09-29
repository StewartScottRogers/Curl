# ADR-0161 — The hand-built Kerberos encrypts with the AES-SHA1, AES-SHA2 and rc4-hmac encryption types from the initial cipher state

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-686.

## Context

The hand-built Kerberos client (ADR-0142's fallback route) must make keys from
passwords, encrypt and decrypt KDC and AP messages, and checksum them with the
encryption types MIT Kerberos and Active Directory still negotiate:
`aes256-cts-hmac-sha1-96` (18), `aes128-cts-hmac-sha1-96` (17) from RFC 3962,
`aes128-cts-hmac-sha256-128` (19), `aes256-cts-hmac-sha384-192` (20) from RFC 8009, and
`rc4-hmac` (23) from RFC 4757. Several choices are left open by the RFCs or are
implementation limits.

## Decision

- **One abstract `KerberosEncryption`, one instance per type.**
  `KerberosEncryption.Create(KerberosEncryptionType, IKerberosRandomSource)` returns it;
  its members are `StringToKey`, `Encrypt`, `Decrypt`, `ComputeChecksum`,
  `VerifyChecksum` and `ComputePseudoRandom`, all taking the base key and a key usage
  and deriving per-usage keys themselves (zeroed after use). The concrete types are
  internal. An encryption type number it does not know throws
  `KerberosCryptographyException` with `UnsupportedEncryptionType`, because the number
  arrives from the network, not from a programmer.
- **The initial, all-zero cipher state only.** Every Kerberos message (RFC 4120) and
  every RFC 4121 GSS-API token encrypts from the initial state; carrying a cipher state
  from one message to the next is used by no caller curl has, so it is not offered.
- **Integrity failures are typed.** A wrong checksum, key or key usage throws
  `KerberosCryptographyException` with `IntegrityCheckFailed`; a ciphertext shorter than
  its confounder and checksum throws `CiphertextTooShort`. The decrypted buffer is
  zeroed before the exception leaves. RFC 8009 checks the HMAC before decrypting, as the
  RFC says; RFC 3962 and RFC 4757 can only check it after.
- **MIT's iteration limit.** String-to-key parameters must be empty (the type's
  default: 4096 for RFC 3962, 32768 for RFC 8009) or four bytes naming 1 to 2^24 - 1
  iterations. Zero (2^32 by RFC 3962) and anything at or past 2^24 throw
  `BadStringToKeyParameters`, as MIT Kerberos returns `KRB5_ERR_BAD_S2K_PARAMS`; a
  spoofed KDC reply cannot make the client spin for hours.
- **Passwords are strings, salts are bytes.** AES types feed PBKDF2 the password's
  UTF-8 bytes (RFC 3961 section 3); `rc4-hmac` hashes its UTF-16LE bytes with MD4 and
  ignores the salt and parameters (RFC 4757 section 2). Salts stay bytes because RFC
  3962's own vectors include a binary salt.
- **`rc4-hmac` key usages map to RFC 4757 message types as MIT does**
  (`krb5int_arcfour_translate_usage`): 3 and 9 to 8, 23 to 13, every other usage to
  itself. The export variant `rc4-hmac-exp` (24) is not built: no KDC issues it.
- **The PRF is included** for every type: RFC 3961 section 5.3's simplified-profile PRF
  for RFC 3962, RFC 8009 section 5's KDF, and RFC 4757's HMAC-SHA-1, because FAST and
  the GSS-API PRF need it.
- **Primitives:** AES-CBC-CTS is `Curl.Cryptography.AesCbcCts` (ADR-0118), MD4 and RC4
  are `Curl.Cryptography.Md4` and `Rc4`; PBKDF2, AES, HMAC-SHA-1/256/384, HMAC-MD5, MD5
  and SHA-1 are the BCL's. `Curl.Kerberos.UnitLibrary` now references
  `Curl.Cryptography.UnitLibrary`.
- **Confounders come from `IKerberosRandomSource`**, whose production
  `SystemKerberosRandomSource` is `RandomNumberGenerator.Fill`, so RFC 8009's sample
  encryptions reproduce byte for byte in tests.

## Consequences

Every RFC 3961 A.1 n-fold vector, RFC 3962 appendix B string-to-key and AES-CTS vector,
and RFC 8009 appendix A vector is pinned in `Curl.Kerberos.UnitTests`. RFC 3962 and
RFC 4757 publish no whole-message encryption vectors, so those are pinned by rebuilding
the message layout from BCL primitives in the test, plus round trips and tamper tests.
A caller that someday needs chained cipher state, or `des3-cbc-sha1-kd` and the DES
types, adds it here as a new task.

## Alternatives considered

- **A public class per encryption type.** More surface for no caller: everything that
  uses an encryption type picks it by number from a ticket or KDC reply.
- **Return `false` from `Decrypt` on an integrity failure.** Inconsistent with the
  library's other typed failures (`KerberosFileException`,
  `KerberosConfigurationException`), and a byte array plus a flag is easier to misuse.
- **Honour 2^32 iterations.** RFC 3962 allows it and itself suggests a limit of at
  least 50,000; MIT's 2^24 - 1 is what the Kerberos curl links against enforces.
