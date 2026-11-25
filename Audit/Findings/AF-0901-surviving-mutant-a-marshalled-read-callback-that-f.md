---
id: AF-0901
title: Surviving mutant: a marshalled read callback that fills the whole buffer can upload nothing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
key: quality:Curl.Engine.UnitLibrary/TransferCallbackMarshal.cs:ReadThrough-le:surviving-mutant
reproduction: none
task: BL-5589
tasks: BL-5589
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_0456.md
---
# AF-0901 - Surviving mutant: a marshalled read callback that fills the whole buffer can upload nothing

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferCallbackMarshal.cs:67`: Surviving mutant: a marshalled read callback that fills the whole buffer can upload nothing.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferCallbackMarshal.cs:67`

Mutant if (count > 0 && count <= filled.Length) -> count < filled.Length survived Curl.Engine.UnitTests (seeded sample, seed 622456). ReadThrough runs the read callback on the caller's thread into a copy, then copies the bytes into the transfer's buffer. Mutated, a callback that returns exactly the buffer's length (the normal case for a large upload) reports that count, but its bytes are never copied. The request body sent is whatever was in the buffer, so the request bytes are wrong. No test reads a full buffer through the marshal.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCallbackMarshal.cs:67:<= -Member ReadThrough -ExcludeBaselineFailures -TimeoutSeconds 400
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-20 | 2026-11-20_0456.md | reproduces: no | Ran the -Site command: TransferCallbackMarshal.cs:67 '<=' (count <= filled.Length -> <) in ReadThrough, resolvedLine 67, outcome killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
