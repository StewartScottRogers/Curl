---
id: AF-0037
title: Certificate name lookup `forIssuer: false` can become true with no test failing
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TlsFailureMessages.cs:SubjectNameLookup-forIssuer:surviving-mutant
task: BL-1380
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed:
closed-by:
---
# AF-0037 - Certificate name lookup `forIssuer: false` can become true with no test failing

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`: Certificate name lookup `forIssuer: false` can become true with no test failing. Reported by an auditor flagged unreliable in 2026-10-03_1459.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`

Mutant `GetNameInfo(SimpleName, forIssuer: true)` survived at seed 0. The subject name feeds a TLS failure message, so the mutant would print the issuer's name where the subject's belongs. No test tells the two apart.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at TlsFailureMessages.cs:320 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320 false

## Re-audits

- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | Same two sites as AF-0031: the TlsFailureMessages.cs:320 mutant is now killed, but SchannelCommonNameCheck.cs:30 'forIssuer: false' -> 'true' still survives (all 2996 filtered Networking tests pass), because every test certificate is self-signed.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
