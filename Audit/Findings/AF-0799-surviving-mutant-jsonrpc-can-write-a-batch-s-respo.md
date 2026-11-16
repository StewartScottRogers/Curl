---
id: AF-0799
title: Surviving mutant: --jsonrpc can write a batch's responses out of call order when the first one received answers an early call
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs:Paired-ge:surviving-mutant
reproduction: mutation Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs:49:>=
task: BL-5250
tasks: BL-5250
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0799 - Surviving mutant: --jsonrpc can write a batch's responses out of call order when the first one received answers an early call

## Summary

High finding from the quality auditor at `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs:49`: Surviving mutant: --jsonrpc can write a batch's responses out of call order when the first one received answers an early call.

## Evidence

Location: `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs:49`

The seed 770877 sample of the drawn library Curl.Extension.JsonRpc.UnitLibrary: 'if (index >= 0)' -> 'if (index > 0)' in Paired survived. With the mutant, a response found at index 0 is not written in its call's place. It stays in the list and is written later among the unanswered ones. Responses [id1, id3, id2] to calls 1, 2, 3 then print id2, id3, id1 instead of id1, id2, id3, so standard output differs. Every test puts the index-0 match on the last call (Pair_ABatchAnsweredOutOfOrder: [id3, err, id2, id1]; the hook test: [id2, id1]), where the unpaired tail happens to give the same order.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.JsonRpc.UnitLibrary/JsonRpcReplyPairing.cs:49:>= -Member Paired -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
