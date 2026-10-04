---
id: BL-1266
title: Fix AF-0010: Cryptography tests EncryptCbc_EmptySource_WritesNothing and Constructor_56ByteKey_IsAccepted (Blowfish, Cast128, Camellia, ChaCha20, Rc4) have no assertion
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1266 — Fix AF-0010: Cryptography tests EncryptCbc_EmptySource_WritesNothing and Constructor_56ByteKey_IsAccepted (Blowfish, Cast128, Camellia, ChaCha20, Rc4) have no assertion

## Goal

The defect the audit office reported as AF-0010 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0010 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0010-cryptography-tests-encryptcbc-emptysource-writesno.md`.

Location: `Curl.Cryptography.UnitTests/BlowfishTests.cs:144`

Location: `Curl.Cryptography.UnitTests/BlowfishTests.cs:144`

The body only calls EncryptCbc/DecryptCbc on empty spans (and the constructor tests only construct and dispose); nothing is asserted. 'WritesNothing' is promised but not checked, and the same pattern is in CamelliaTests.cs:78, Cast128Tests.cs:137,156, ChaCha20Tests.cs:151 and Rc4Tests.cs:344. They pass for any implementation that does not throw.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitTests/BlowfishTests.cs -Pattern 'EncryptCbc_EmptySource_WritesNothing' -Context 0,6
```

- Expected: The method body contains an Assert.* call.
- Actual: The body holds two calls and no assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix, so done directly rather than through the full `/feature` stages (no production code, no plan to make).
- Empty-source tests (Blowfish, Camellia, Cast128 CBC; ChaCha20 keystream): the destination must equal the source's length, so it is a zero-length slice of an 8-byte `0xA5` sentinel buffer, and the test asserts the buffer is still all `0xA5` - "writes nothing" is now checked.
- Key-size-accepted tests (Blowfish 56 bytes, Cast128 5 and 16, Rc4 1 and 256): each now asserts the accepted key actually works - an all-zero block or 16 bytes encrypt to something different and decrypt (or, for RC4, a second same-keyed instance's keystream) back to the original.
- Verified: `dotnet build` 0 warnings, 0 errors; fast tests exit 0 across 33 test projects (Curl.Cryptography.UnitTests 1330 passed).

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Six assertion-free cryptography tests now assert: empty-source calls leave a sentinel buffer untouched, accepted keys round-trip a block
