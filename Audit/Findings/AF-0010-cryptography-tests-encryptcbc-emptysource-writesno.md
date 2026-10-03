---
id: AF-0010
title: Cryptography tests EncryptCbc_EmptySource_WritesNothing and Constructor_56ByteKey_IsAccepted (Blowfish, Cast128, Camellia, ChaCha20, Rc4) have no assertion
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Cryptography.UnitTests/BlowfishTests.cs:EncryptCbc_EmptySource_WritesNothing:no-assertion
task: BL-1266
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0010 - Cryptography tests EncryptCbc_EmptySource_WritesNothing and Constructor_56ByteKey_IsAccepted (Blowfish, Cast128, Camellia, ChaCha20, Rc4) have no assertion

## Summary

Low finding from the quality auditor at `Curl.Cryptography.UnitTests/BlowfishTests.cs:144`: Cryptography tests EncryptCbc_EmptySource_WritesNothing and Constructor_56ByteKey_IsAccepted (Blowfish, Cast128, Camellia, ChaCha20, Rc4) have no assertion. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `Curl.Cryptography.UnitTests/BlowfishTests.cs:144`

The body only calls EncryptCbc/DecryptCbc on empty spans (and the constructor tests only construct and dispose); nothing is asserted. 'WritesNothing' is promised but not checked, and the same pattern is in CamelliaTests.cs:78, Cast128Tests.cs:137,156, ChaCha20Tests.cs:151 and Rc4Tests.cs:344. They pass for any implementation that does not throw.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitTests/BlowfishTests.cs -Pattern 'EncryptCbc_EmptySource_WritesNothing' -Context 0,6
```

- Expected: The method body contains an Assert.* call.
- Actual: The body holds two calls and no assertion.

## Re-audits

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
