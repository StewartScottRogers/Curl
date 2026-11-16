---
id: AF-0827
title: Surviving mutant: a QUIC dial whose TLS setup fails can go on to the next address
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/QuicDialer.cs:DialThroughTunnelAsync-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/QuicDialer.cs:197:false
task: none
tasks:
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0827 - Surviving mutant: a QUIC dial whose TLS setup fails can go on to the next address

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/QuicDialer.cs:197`: Surviving mutant: a QUIC dial whose TLS setup fails can go on to the next address. Reappeared; previously AF-0638. Reported by an auditor flagged unreliable in 2026-11-16_1333.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:197`

Mutation sample (seed 413266): '? (preparationFailure!, false)' mutated to '? (preparationFailure!, true)' survived. The bool is the attempt's TryNextAddress (QuicDialer.cs:182). With the mutant, a TLS preparation failure (a bad --cert, an unusable cipher list), which cannot succeed on any address, makes the dialer try every next address: one more 'Trying' line and failed attempt per address under -v. No test dials a host with two addresses and a TLS setup failure.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/QuicDialer.cs:197:false -Member DialThroughTunnelAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
