---
id: AF-0741
title: Surviving mutant: curl_multi_socket_action without a socket callback can answer CURLM_ABORTED_BY_CALLBACK
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:UpdateSockets-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:153:true
task: none
tasks:
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-13: filed proposed.
