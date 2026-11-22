---
id: AF-0966
title: Surviving mutant: removing a handle from a multi can drop every other handle's done message and keep its own (== -> !=)
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs:RemoveEntry-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs:136:==
task: none
tasks:
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0966 - Surviving mutant: removing a handle from a multi can drop every other handle's done message and keep its own (== -> !=)

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs:136`: Surviving mutant: removing a handle from a multi can drop every other handle's done message and keep its own (== -> !=).

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs:136`

Seeded sample (seed 51850, -MaxMutants 40): the mutant at ManagedMultiTransfer.Queueing.cs:136 operator '==' survived. Original: 'messages.RemoveAll(message => message.Transfer == entry.Transfer);'; mutated: '!='. RemoveEntry runs for curl_multi_remove_handle (ManagedMultiTransfer.Entries.cs:96). With the mutant, removing one handle deletes the pending CURLMSG_DONE messages of every other handle and keeps the removed handle's own. curl_multi_info_read then reports a handle that is no longer added and loses the others. No multi test removes one handle while another handle's message is still unread.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs:136:== -Member RemoveEntry -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test removes one of two finished handles and still reads the other's message
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-22: filed proposed.
