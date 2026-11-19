---
id: AF-0879
title: Surviving mutant: a STOMP blank line at the start of a refilled buffer can read buffer[-1]
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:ReadLineAsync-gt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:80:>
task: BL-5543
tasks: BL-5543
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0879 - Surviving mutant: a STOMP blank line at the start of a refilled buffer can read buffer[-1]

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:80`: Surviving mutant: a STOMP blank line at the start of a refilled buffer can read buffer[-1].

## Evidence

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:80`

Mutant 'int length = newline >= start && buffer[newline - 1] == '\r' ? ...' (from '>') survived in the sample and in the targeted -Site run. FillAsync compacts the buffer so that start = 0 (lines 130-134). When the header block's ending blank line '\n' arrives as the first byte of a refill, newline == start == 0. The mutant then reads buffer[-1] and throws IndexOutOfRangeException, ending a transfer that should succeed with a different exit code. ReadAsync_FrameSplitAcrossReads_IsReassembled does not split the frame at the blank line.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:80:> -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (resolvedLine 80)

## Re-audits

## Log

- 2026-11-19: filed proposed.
