---
id: BL-1378
title: Fix AF-0035: AES-CCM length-field check `lengthFieldSize < sizeof(int)` can become `<=` with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1378 — Fix AF-0035: AES-CCM length-field check `lengthFieldSize < sizeof(int)` can become `<=` with no test failing

## Goal

The defect the audit office reported as AF-0035 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0035 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0035-aes-ccm-length-field-check-lengthfieldsize-sizeof.md`.

Location: `Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168`

Location: `Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168`

Mutant `lengthFieldSize <= sizeof(int)` survived at seed 0. With an 11-byte nonce the length field is 4 bytes, so `sourceLength >> 32` is a shift by 0 in C# and the mutant would reject every non-empty payload. No test uses an 11-byte nonce with a non-empty payload.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Cryptography.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-crypto.json
```

- Expected: The mutant at AeadAesCcm.cs:168 is killed.
- Actual: survived  Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168 <

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right; the gap was a test. Added
  `AeadAesCcmTests.EncryptAndTryDecrypt_ElevenByteNonce_MatchTheBclCiphertextAndTag`:
  NIST SP 800-38C's key, its nonce cut to 11 bytes (a 4-byte length field), 20 bytes of
  associated data and the 24-byte payload, with ciphertext and tag pinned to the BCL's
  `AesCcm` on Windows (2026-10-03). No published vector uses an 11-byte nonce, so the BCL
  is the reference, as the file's other BCL-pinned tests do.
- Added `Curl.Cryptography.UnitTests` to `touches`: the fix is a test. No other task in
  Doing on `origin/work/dark-factory` names it.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (the audit-path guard), so
  the mutant was checked by hand: with `lengthFieldSize <= sizeof(int)` at
  `AeadAesCcm.cs:168` the new test fails (Encrypt throws `ArgumentException`); with the
  original `<` it passes. The quality auditor's re-audit confirms it with the tool.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An 11-byte-nonce AES-CCM test pinned to the BCL kills the AF-0035 mutant at AeadAesCcm.cs:168
