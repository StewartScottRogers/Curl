---
id: AF-0741
title: Surviving mutant: curl_multi_socket_action without a socket callback can answer CURLM_ABORTED_BY_CALLBACK
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:UpdateSockets-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:153:true
task: BL-5186
tasks: BL-5186
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1335.md
---
# AF-0741 - Surviving mutant: curl_multi_socket_action without a socket callback can answer CURLM_ABORTED_BY_CALLBACK

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:153`: Surviving mutant: curl_multi_socket_action without a socket callback can answer CURLM_ABORTED_BY_CALLBACK.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:153`

Mutant return true; -> return false; in UpdateSockets (the branch where Options.Socket is null) survived (seed 641133). Its caller (line 66) is return !socketAction || UpdateSockets() ? UpdateTimer(timerFired) : CurlMultiCode.AbortedByCallback;, so a libcurl user driving the multi handle with socket_action and no CURLMOPT_SOCKETFUNCTION would get AbortedByCallback instead of Ok. No test calls SocketActionAsync on a multi without a Socket callback.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:153:true -Member UpdateSockets -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-14 | 2026-11-14_1335.md | reproduces: no | Ran the -Site command: ManagedMultiTransfer.MovingOn.cs:153 'true' mutant outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-13: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
