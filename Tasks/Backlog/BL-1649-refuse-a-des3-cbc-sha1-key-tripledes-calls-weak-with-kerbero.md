---
id: BL-1649
title: Refuse a des3-cbc-sha1 key TripleDES calls weak with KerberosCryptographyException, not CryptographicException
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-1501]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1649 — Refuse a des3-cbc-sha1 key TripleDES calls weak with KerberosCryptographyException, not CryptographicException

## Goal

`KerberosEncryption.Create(KerberosEncryptionType.Des3CbcSha1, ...)`'s `Encrypt`, `Decrypt`, `ComputeChecksum` and `ComputePseudoRandom` never let a `System.Security.Cryptography.CryptographicException` escape for a 24-byte base key that .NET's `TripleDES` calls weak (its first and second, or second and third, 8-byte parts equal, e.g. all zeros): they either compute RFC 3961's answer as MIT does or refuse with a documented `KerberosCryptographyException`.

## Context

- Found by BL-1501's adversarial tests: `Encrypt(new byte[24], 1, [])` on the des3 type throws `CryptographicException: Specified key is a known weak key for 'TripleDES' and cannot be used.` from `TripleDES.set_Key` in `Des3CbcSha1KerberosEncryption.DeriveRandom` (Curl.Kerberos.UnitLibrary/Des3CbcSha1KerberosEncryption.cs, `tripleDes.SetKey(key)`). The derived keys go through the same `SetKey` too.
- The base key comes from a peer (a KDC reply's session key, a keytab, a credential cache), so a hostile or odd key crashes the caller with an exception no doc comment promises. MIT's own des3 code (lib/crypto/builtin/des) uses the key as given, without .NET's weak-key check; matching MIT means encrypting with such a key rather than refusing, which needs 3DES-EDE built without `TripleDES.SetKey`'s check (e.g. three single `DES` passes, though `DES` has its own weak-key check, or a hand-built DES in the library) - decide, and record the decision in an ADR if it refuses instead.
- High: an undocumented exception escaping from peer-controlled input is a crash.

## Acceptance criteria

- [ ] A test in `Curl.Kerberos.UnitTests` encrypts and decrypts with an all-zero 24-byte des3 key and with one whose first two 8-byte parts are equal, and gets either a round trip or a `KerberosCryptographyException` with a named error, never a `CryptographicException`.
- [ ] `dotnet build` is clean and the fast tests pass; the library keeps its 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 5 could not integrate: fast tests failed twice (Curl.Cookies.UnitTests: EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother; then Curl.Cookies.UnitTests failed) after rebasing onto the other lanes' work. The work is on branch factory/BL-1649-lane-5-20261007-111121; start with git cherry-pick --no-commit factory/BL-1649-lane-5-20261007-111121 and fix it.
