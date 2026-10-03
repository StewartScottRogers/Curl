---
id: BL-1378
title: Fix AF-0035: AES-CCM length-field check `lengthFieldSize < sizeof(int)` can become `<=` with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
