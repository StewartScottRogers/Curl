---
id: AF-0420
title: SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close
auditor: quality
severity: Medium
status: accepted
reason: Stewart accepted it on 2026-10-24.
key: quality:Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:Send-true:surviving-mutant
reproduction: none
task: BL-4449
tasks: BL-3469, BL-4449
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0420 - SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441`: SwsHttpServerConnection.Send's closing SwsServerSend(..., true) can become false with no test failing, so a closing reply never marks the close.

## Evidence

Location: `Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'pendingSends.Add(new SwsServerSend([], serverBusyUntil, true));' -> 'false'. The third argument is isClose: the send queued after a closing reply (swsclose, close after the reply) is what ends the connection. With false, the stand-in server no longer closes after such a reply. No test checks that the connection ends.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true -Member Send -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-10-24 | 2026-10-24_2106.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null. SwsHttpServerConnection.Send now marks a close by queuing an empty SwsServerSend([], serverBusyUntil) (line 445) and passes no 'true' argument, so the mutant cannot be run. Not re-audited.
- 2026-10-25 | 2026-10-25_0941.md | not re-audited | Ran the -Site reproduction (SwsHttpServerConnection.cs:441:true, -Member Send): outcome site-missing, resolvedLine null, no mutant run. Send now adds 'new SwsServerSend([], serverBusyUntil)' under 'if (closes)', and SwsServerSend has no closing flag left. Not re-audited.
- 2026-10-25 | 2026-10-25_1542.md | not re-audited | Ran the -Site Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true -Member Send command: outcome site-missing. SwsServerSend is now built with no boolean argument (lines 360, 433, 445), so no mutant ran and this is not re-audited.
- 2026-10-25 | 2026-10-25_1944.md | not re-audited | Ran Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true -Member Send -ExcludeBaselineFailures -TimeoutSeconds 600: outcome site-missing (baseline 60078 ms, no mutants). SwsHttpServerConnection.cs no longer has a SwsServerSend(..., true) call (its SwsServerSend constructions at lines 241, 360, 433 and 445 take two arguments). Not re-audited.
- 2026-10-29 | 2026-10-29_0606.md | not re-audited | Ran the reproduction (-Site Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true -Member Send). outcome: site-missing, resolvedLine null. Send (line 424) now marks a close with `pendingSends.Add(new SwsServerSend([], serverBusyUntil));` (line 445) and has no `true` literal to mutate. No mutant could run, so the finding was not re-audited.
- 2026-10-31 | 2026-10-31_0358.md | not re-audited | Ran the -Site reproduction: outcome site-missing (resolvedLine null). SwsHttpServerConnection.Send no longer passes a literal true to SwsServerSend at or near line 441; the closing reply is now driven by the closes parameter (Send(..., reply.ClosesConnection) at line 413). No mutant ran, so not re-audited.
- 2026-10-31 | 2026-10-31_1149.md | not re-audited | Ran the -Site command: 'site-missing Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true', resolvedLine null. Send (line 424) now takes a 'bool closes' parameter, and line 441 is 'if (closes)'; the closing SwsServerSend no longer carries a true literal inside Send. Site missing, so not re-audited.
- 2026-11-01 | 2026-11-01_0144.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null. Send no longer passes a true literal: the closing send is pendingSends.Add(new SwsServerSend([], serverBusyUntil)) under if (closes) (SwsHttpServerConnection.cs:442-447), so the true site does not exist. Not re-audited.
- 2026-11-01 | 2026-11-01_1016.md | not re-audited | Ran the -Site reproduction: outcome site-missing, resolvedLine null. Send now takes a 'bool closes' parameter and has no 'true' literal (lines 424-447); callers pass true at line 400 and reply.ClosesConnection at 413. The baseline left out UpstreamConformanceTests.UpstreamCase_RunThroughCurl_HoldsTheRatchet, which fails unmutated. Not re-audited.
- 2026-11-01 | 2026-11-01_1801.md | not re-audited | Ran powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs:441:true -Member Send -ExcludeBaselineFailures -TimeoutSeconds 600: outcome site-missing, resolvedLine null. Send no longer passes a closing true to SwsServerSend; the close is now an empty SwsServerSend plus RecordDisconnectOnce under if (closes). Not re-audited.

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-11-01: re-fix task BL-4449 filed (Stewart: re-fix them). The mutation site was removed by BL-3469's rewrite, so every re-audit since reported site-missing; reproduction set to none, so the next re-audit judges by reading the code and BL-4449's tests.
