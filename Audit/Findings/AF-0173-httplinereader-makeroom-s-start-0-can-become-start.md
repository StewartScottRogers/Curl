---
id: AF-0173
title: HttpLineReader.MakeRoom's 'start == 0' can become 'start != 0' with no test failing: a status line of 16 KB or more is read as a closed connection
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:MakeRoom-eq:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:163:==
task: none
tasks:
found: 2026-10-12
found-at: 4e6ddbc5b33356117bfec6778ba550ab61454a99
scorecard: 2026-10-12_1653.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0173 - HttpLineReader.MakeRoom's 'start == 0' can become 'start != 0' with no test failing: a status line of 16 KB or more is read as a closed connection

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:163`: HttpLineReader.MakeRoom's 'start == 0' can become 'start != 0' with no test failing: a status line of 16 KB or more is read as a closed connection. Reported by an auditor flagged unreliable in 2026-10-12_1653.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:163`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures reported this mutant survived (5989 ms): 'if (start == 0)' -> 'if (start != 0)' in MakeRoom. With the original code a full buffer is either doubled (start == 0: one line fills it) or compacted (earlier lines taken). With the mutant, a full buffer with start == 0 is 'compacted' (a no-op), so FillAsync reads into a zero-length slice (buffer.AsMemory(end) with end == buffer.Length), gets 0 bytes and returns false, and ReadLineAsync returns null as if the peer had closed. start is 0 with a full buffer whenever the status line itself reaches InitialBufferSize (16384 bytes), well below the measured limit MaximumLineLength = 102400 (BL-169), so such a response becomes an empty or partial reply with a different exit code instead of being read. Header lines still work under the mutant only because it grows the buffer instead of compacting it. No test sends a first line longer than 16 KB, so the gate that decides grow-versus-compact is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:163:== -Member MakeRoom -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test reading a status line longer than 16384 bytes fails under the mutant.
- Actual: outcome survived: every Curl.Protocol.Http.UnitTests test passed with the mutant applied.

## Re-audits

## Log

- 2026-10-12: filed proposed.
- 2026-10-12: proposed -> accepted.
