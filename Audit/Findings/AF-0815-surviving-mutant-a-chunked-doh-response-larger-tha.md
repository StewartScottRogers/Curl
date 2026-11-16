---
id: AF-0815
title: Surviving mutant: a chunked DoH response larger than curl's buffer can be read on instead of refused
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/DohResponseReader.cs:ReadChunkAsync-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DohResponseReader.cs:158:false
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0815 - Surviving mutant: a chunked DoH response larger than curl's buffer can be read on instead of refused

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DohResponseReader.cs:158`: Surviving mutant: a chunked DoH response larger than curl's buffer can be read on instead of refused.

## Evidence

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:158`

In ReadChunkAsync, `return false;` (the refusal when body.Count + size > MaximumBodyLength) changed to `return true;` survived. The reader then goes on without reading the chunk, treating its data as the next size line, instead of failing the DoH resolve as curl does for an oversize answer. No test sends a chunked DoH body past the limit.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohResponseReader.cs:158:false -Member ReadChunkAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
