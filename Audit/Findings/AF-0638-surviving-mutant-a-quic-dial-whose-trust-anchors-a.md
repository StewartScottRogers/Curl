---
id: AF-0638
title: Surviving mutant: a QUIC dial whose trust anchors are unusable can go on to the next address, and no test fails
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/QuicDialer.cs:DialThroughTunnelAsync-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/QuicDialer.cs:210:false
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0638 - Surviving mutant: a QUIC dial whose trust anchors are unusable can go on to the next address, and no test fails

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/QuicDialer.cs:210`: Surviving mutant: a QUIC dial whose trust anchors are unusable can go on to the next address, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:210`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'return (await TrustAnchorsUnusableAsync(...), false);' -> 'true'. The tuple is AttemptAsync's (Result, TryNextAddress), and line 126 acts on tryNextAddress. With unusable CA settings, the mutant retries every remaining address instead of stopping at the first refusal, which changes the -v lines and the time and attempts before exit. No test dials a name with two addresses under unusable trust anchors.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/QuicDialer.cs:210:false -Member DialThroughTunnelAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
