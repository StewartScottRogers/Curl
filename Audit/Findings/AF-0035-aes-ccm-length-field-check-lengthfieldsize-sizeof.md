---
id: AF-0035
title: AES-CCM length-field check `lengthFieldSize < sizeof(int)` can become `<=` with no test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:RequireMessageLength:surviving-mutant
task: none
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed:
closed-by:
---
# AF-0035 - AES-CCM length-field check `lengthFieldSize < sizeof(int)` can become `<=` with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168`: AES-CCM length-field check `lengthFieldSize < sizeof(int)` can become `<=` with no test failing. Reported by an auditor flagged unreliable in 2026-10-03_1459.md.

## Evidence

Location: `Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168`

Mutant `lengthFieldSize <= sizeof(int)` survived at seed 0. With an 11-byte nonce the length field is 4 bytes, so `sourceLength >> 32` is a shift by 0 in C# and the mutant would reject every non-empty payload. No test uses an 11-byte nonce with a non-empty payload.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Cryptography.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-crypto.json
```

- Expected: The mutant at AeadAesCcm.cs:168 is killed.
- Actual: survived  Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168 <

## Re-audits

## Log

- 2026-10-03: filed proposed.
