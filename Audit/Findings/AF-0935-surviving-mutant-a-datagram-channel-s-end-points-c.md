---
id: AF-0935
title: Surviving mutant: a datagram channel's end points can be re-recorded on every datagram, not once
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:ReceiveAsync-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:65:true
task: BL-5694
tasks: BL-5694
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_1740.md
---
# AF-0935 - Surviving mutant: a datagram channel's end points can be re-recorded on every datagram, not once

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:65`: Surviving mutant: a datagram channel's end points can be re-recorded on every datagram, not once. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:65`

Seed 812510 sample: _recorded = true; mutated to _recorded = false; survived. The class comment says the end points are recorded again once a datagram has been received; the mutant re-records on every receive, so a channel that moves again changes the reported end points (%{remote_ip}, %{local_port}). No test receives twice after a move.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:65:true -Member ReceiveAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_1740.md | reproduces: no | Ran the -Site command: resolvedLine 65, mutant '_recorded = false;' killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-21: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
