---
id: AF-0804
title: Surviving mutant: curl_multi_socket_action can signal the wrong events to a waiting transfer
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs:--eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs:60:==
task: none
tasks:
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0804 - Surviving mutant: curl_multi_socket_action can signal the wrong events to a waiting transfer

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs:60`: Surviving mutant: curl_multi_socket_action can signal the wrong events to a waiting transfer.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs:60`

The seed 770877 sample of Curl.Engine.UnitLibrary (one of the three largest): 'Signal(events == CurlSocketSelect.None ? SelectOf(entry.SocketWait.Waiting) : events)' -> 'events != CurlSocketSelect.None ? ...' in SocketActionAsync survived. The tool named the member '-' because the tuple return type hid the declaration. With the mutant, an application passing CURL_CSELECT_IN/OUT has the transfer woken with what it waits for, not what happened. One passing 0 (check state) has it signalled with no events, which can leave it waiting. No test drives socket_action with both a zero and a non-zero event mask and checks what the transfer saw.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs:60:== -Member - -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
