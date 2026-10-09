---
id: BL-1869
title: Fix AF-0119: Sntrup761Ring.Decrypt's fallback 'index < W' can become '<= W' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1869 — Fix AF-0119: Sntrup761Ring.Decrypt's fallback 'index < W' can become '<= W' with no test failing

## Goal

The defect the audit office reported as AF-0119 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0119 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0119-sntrup761ring-decrypt-s-fallback-index-w-can-becom.md`.

Location: `Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:238`

Location: `Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:238`

Mutant 'int fallback = index < W ? 1 : 0;' -> 'index <= W' survived. The fallback is the fixed-weight vector NTRU Prime returns when the decrypted vector's weight is wrong (mask != 0), i.e. for an invalid ciphertext. The mutant makes it weight W+1, so decapsulating an invalid or tampered ciphertext gives a different implicit-rejection secret than the reference. The Round 3 known-answer test only decapsulates valid ciphertexts, so the rejection path is never compared byte for byte. In SSH the key exchange fails either way, so this is a decision that does not reach output directly.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:238:< -Member Decrypt -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- New `Curl.Cryptography.UnitTests/Sntrup761RingTests.cs` (the test project of the library in
  `touches`, no other task affected): `Decrypt_DecryptionWithoutWeightW_GivesWOnesThenZeros`
  decrypts an all-zero ciphertext (weight 0, so the fallback is taken) and pins the result to
  exactly W ones then P - W zeros.
- The lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit paths are guarded from
  lanes), so the mutant was applied by hand: with `index <= W` the new test fails
  ("Element at index 286 do not match"); reverted, it passes. The quality auditor's re-audit
  confirms it with the tool.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Test pins Decrypt's weight-w fallback; the index <= W mutant now fails it
