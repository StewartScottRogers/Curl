---
id: AF-0847
title: Surviving mutant: a QUIC dial with exactly no connect time left still tries the next address
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/QuicDialer.cs:DialAsync-le:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/QuicDialer.cs:133:<=
task: BL-5367
tasks: BL-5367
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0847 - Surviving mutant: a QUIC dial with exactly no connect time left still tries the next address

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/QuicDialer.cs:133`: Surviving mutant: a QUIC dial with exactly no connect time left still tries the next address.

## Evidence

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:133`

if (remaining <= TimeSpan.Zero) -> < TimeSpan.Zero survived. When the connect timeout is exactly used up, the dialer goes on to attempt the address with a zero budget instead of returning TimedOut(request) at once. The clock is injected, so the boundary is testable.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/QuicDialer.cs:133:<= -Member DialAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-17: filed proposed.
