---
id: AF-0917
title: Surviving mutant: StreamConnection counts a zero-byte (end-of-stream) read as having read bytes
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/StreamConnection.cs:ReadAsync-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/StreamConnection.cs:128:>
task: none
tasks:
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0917 - Surviving mutant: StreamConnection counts a zero-byte (end-of-stream) read as having read bytes

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/StreamConnection.cs:128`: Surviving mutant: StreamConnection counts a zero-byte (end-of-stream) read as having read bytes.

## Evidence

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:128`

`_hasReadBytes |= read > 0;` mutated to `read >= 0` survived Curl.Networking.UnitTests. _hasReadBytes gates whether an unread FIN at the last write keeps a later WSAECONNABORTED as an abort or reports it as ConnectionReset (ADR-0878, BL-5259: an unread FIN is not counted until the connection has read a byte). With the mutant a fresh connection whose only read returned 0 counts as having read, so the reset/abort decision, and the exit code and message curl prints, change on Windows. No test reads a zero-byte end before the write that decides it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/StreamConnection.cs:128:> -Member ReadAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-20: filed proposed.
