# ADR-0423 — A des3-cbc-sha1 key triple DES calls weak is refused as `KerberosCryptographyError.WeakKey`

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1649
- Decided by Claude under Stewart's delegation.

## Context

BL-1501's adversarial tests found that `KerberosEncryption.Create(KerberosEncryptionType.Des3CbcSha1, ...)`
let .NET's `CryptographicException` ("Specified key is a known weak key for 'TripleDES'") escape
from `Encrypt`, `Decrypt`, `ComputeChecksum` and `ComputePseudoRandom` when the 24-byte base key
has equal first and second, or second and third, 8-byte parts (parity bits aside), such as an
all-zero key. Such a key is single DES. The base key comes from a peer - a KDC reply's session
key, a keytab, a credential cache - so the escape was an undocumented crash on peer input.

MIT Kerberos uses such a key as given. Matching it means 3DES-EDE without `TripleDES.SetKey`'s
check: three single-DES passes do not work, because .NET's `DES` refuses DES weak keys (an
all-zero key among them), so it would need a hand-built DES in the library.

## Decision

Refuse. `Des3CbcSha1KerberosEncryption.CreateTripleDes`, the one place both the encryption type
and `Des3CbcSha1GssMessageProtection` make a triple DES cipher, compares the key's parts with
their parity bits masked, the rule .NET applies, and throws `KerberosCryptographyException` with
the new `KerberosCryptographyError.WeakKey` before .NET's own check can. Derived keys go through
the same factory, so the 2^-52 chance that a derived key is weak (ADR-0237) is refused the same
way. The check is Curl's own, so the answer does not depend on how a platform's `TripleDES`
validates keys.

## Why

- A KDC never issues a key whose triple DES collapses to single DES: MIT's own random-to-key
  makes three independent parts, and des3 is deprecated (RFC 8429). No real exchange carries
  one, so refusing it costs no drop-in compatibility a script can observe.
- A hand-built DES would be a second cipher implementation, with its own coverage and constant-time
  burden, to accept only keys that are already broken. The simplest thing that stays a drop-in
  replacement is a documented refusal.
- A named error keeps every public method's exception contract true: they throw only what their
  doc comments list.

## Consequences

- `Encrypt`, `Decrypt`, `ComputeChecksum`, `VerifyChecksum` and `ComputePseudoRandom` document
  `KerberosCryptographyError.WeakKey` for des3; no `CryptographicException` escapes them for any
  24-byte key.
- If a real peer is ever found sending such a key, this is revisited with a hand-built DES in its
  own `UnitLibrary`.
