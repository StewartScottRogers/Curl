---
id: AF-0035
title: AES-CCM length-field check `lengthFieldSize < sizeof(int)` can become `<=` with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:RequireMessageLength-lt:surviving-mutant
reproduction: mutation Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168:<
task: BL-1378
tasks: BL-1378
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
duplicate-of:
closed: 2026-10-07
closed-how: mechanical
closed-by: 2026-10-07_1336.md
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
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168:< -Member RequireMessageLength -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at AeadAesCcm.cs:168 is killed.
- Actual: survived  Curl.Cryptography.UnitLibrary/AeadAesCcm.cs:168 <

## Re-audits

- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Not in the seed-0 tool sample at this commit, so applied by hand in a scratch export: AeadAesCcm.cs:168 'lengthFieldSize < sizeof(int)' -> '<='. Killed by EncryptAndTryDecrypt_ElevenByteNonce_MatchTheBclCiphertextAndTag.
- 2026-10-07 | 2026-10-07_1336.md | reproduces: no | Ran the -Site reproduction: resolvedLine 168, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
