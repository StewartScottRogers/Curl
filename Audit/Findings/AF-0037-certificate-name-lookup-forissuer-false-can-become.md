---
id: AF-0037
title: Certificate name lookup `forIssuer: false` can become true with no test failing
auditor: quality
severity: High
status: closed
reason: Duplicate of AF-0031: the same surviving mutant (TlsFailureMessages.cs OpenSslPeerFailedVerification, forIssuer false -> true), whose mechanical key both now carry (ADR-0422).
key: quality:Curl.Networking.UnitLibrary/TlsFailureMessages.cs:OpenSslPeerFailedVerification-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320:false
task: BL-1380
tasks: BL-1380, BL-1608
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
duplicate-of: AF-0031
closed: 2026-10-07
closed-how: duplicate
closed-by: session
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
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320:false -Member OpenSslPeerFailedVerification -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at TlsFailureMessages.cs:320 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320 false

## Re-audits

- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | Same two sites as AF-0031: the TlsFailureMessages.cs:320 mutant is now killed, but SchannelCommonNameCheck.cs:30 'forIssuer: false' -> 'true' still survives (all 2996 filtered Networking tests pass), because every test certificate is self-signed.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
- 2026-10-07: accepted -> closed. Duplicate of AF-0031: the same surviving mutant (TlsFailureMessages.cs OpenSslPeerFailedVerification, forIssuer false -> true), whose mechanical key both now carry (ADR-0422).
