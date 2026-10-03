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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
