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
completed: 2026-10-07
---
# BL-1649 — Refuse a des3-cbc-sha1 key TripleDES calls weak with KerberosCryptographyException, not CryptographicException

## Goal

`KerberosEncryption.Create(KerberosEncryptionType.Des3CbcSha1, ...)`'s `Encrypt`, `Decrypt`, `ComputeChecksum` and `ComputePseudoRandom` never let a `System.Security.Cryptography.CryptographicException` escape for a 24-byte base key that .NET's `TripleDES` calls weak (its first and second, or second and third, 8-byte parts equal, e.g. all zeros): they either compute RFC 3961's answer as MIT does or refuse with a documented `KerberosCryptographyException`.

## Context

- Found by BL-1501's adversarial tests: `Encrypt(new byte[24], 1, [])` on the des3 type throws `CryptographicException: Specified key is a known weak key for 'TripleDES' and cannot be used.` from `TripleDES.set_Key` in `Des3CbcSha1KerberosEncryption.DeriveRandom` (Curl.Kerberos.UnitLibrary/Des3CbcSha1KerberosEncryption.cs, `tripleDes.SetKey(key)`). The derived keys go through the same `SetKey` too.
- The base key comes from a peer (a KDC reply's session key, a keytab, a credential cache), so a hostile or odd key crashes the caller with an exception no doc comment promises. MIT's own des3 code (lib/crypto/builtin/des) uses the key as given, without .NET's weak-key check; matching MIT means encrypting with such a key rather than refusing, which needs 3DES-EDE built without `TripleDES.SetKey`'s check (e.g. three single `DES` passes, though `DES` has its own weak-key check, or a hand-built DES in the library) - decide, and record the decision in an ADR if it refuses instead.
- High: an undocumented exception escaping from peer-controlled input is a crash.

## Acceptance criteria

- [x] A test in `Curl.Kerberos.UnitTests` encrypts and decrypts with an all-zero 24-byte des3 key and with one whose first two 8-byte parts are equal, and gets either a round trip or a `KerberosCryptographyException` with a named error, never a `CryptographicException`.
- [x] `dotnet build` is clean and the fast tests pass; the library keeps its 100% line and branch coverage.

## Notes

- Decision (ADR-0424, decided by Claude under Stewart's delegation): refuse rather than hand-build DES. A key whose first and second, or second and third, 8-byte parts are equal apart from parity is single DES; no KDC issues one and des3 is deprecated, so refusing costs no observable compatibility. Three single-DES passes could not match MIT either: .NET's DES refuses DES weak keys such as all zeros.
- `Des3CbcSha1KerberosEncryption.CreateTripleDes` is the one factory for triple DES, used by the encryption type and `Des3CbcSha1GssMessageProtection`; it checks the parts itself (parity masked) and throws `KerberosCryptographyException(KerberosCryptographyError.WeakKey)` before .NET's check. Derived keys go through it too. Public doc comments list `WeakKey`.
- Tests: `EncryptDecryptChecksumAndPseudoRandom_WeakKey_ThrowWeakKey` (all zeros, first two parts equal, last two equal, parity-only difference) and `CreateTripleDes_StrongKey_EncryptsAsTripleDes`. Kerberos tests 781 green.
- Coverage: lane 5's Measure-CodeQuality run gave Curl.Kerberos.UnitLibrary 100% line, 100% branch, 0 failing members; lanes 1 and 7 resumed the same code by cherry-pick (lane 7 from lane 1's commit 006dc5418) unchanged, so it was not re-measured.
- Lane 7: both earlier integration failures were outside this task (the Cookies concurrency flake BL-1651 owns, and an unnamed failure). On lane 7 the whole fast run is green: every test project passed, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 5 could not integrate: fast tests failed twice (Curl.Cookies.UnitTests: EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother; then Curl.Cookies.UnitTests failed) after rebasing onto the other lanes' work. The work is on branch factory/BL-1649-lane-5-20261007-111121; start with git cherry-pick --no-commit factory/BL-1649-lane-5-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 1 could not integrate: fast tests failed twice (no test named; then no test named) after rebasing onto the other lanes' work. The work is on branch factory/BL-1649-lane-1-20261007-111121; start with git cherry-pick --no-commit factory/BL-1649-lane-1-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. des3-cbc-sha1 refuses a key triple DES calls weak with KerberosCryptographyError.WeakKey; no CryptographicException escapes
