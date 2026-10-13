---
id: BL-2119
title: Fix AF-0173: HttpLineReader.MakeRoom's 'start == 0' can become 'start != 0' with no test failing: a status line of 16 KB or more is read as a closed connection
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-13
completed:
---
# BL-2119 — Fix AF-0173: HttpLineReader.MakeRoom's 'start == 0' can become 'start != 0' with no test failing: a status line of 16 KB or more is read as a closed connection

## Goal

The defect the audit office reported as AF-0173 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0173 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0173-httplinereader-makeroom-s-start-0-can-become-start.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:163`

Location: `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:163`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures reported this mutant survived (5989 ms): 'if (start == 0)' -> 'if (start != 0)' in MakeRoom. With the original code a full buffer is either doubled (start == 0: one line fills it) or compacted (earlier lines taken). With the mutant, a full buffer with start == 0 is 'compacted' (a no-op), so FillAsync reads into a zero-length slice (buffer.AsMemory(end) with end == buffer.Length), gets 0 bytes and returns false, and ReadLineAsync returns null as if the peer had closed. start is 0 with a full buffer whenever the status line itself reaches InitialBufferSize (16384 bytes), well below the measured limit MaximumLineLength = 102400 (BL-169), so such a response becomes an empty or partial reply with a different exit code instead of being read. Header lines still work under the mutant only because it grows the buffer instead of compacting it. No test sends a first line longer than 16 KB, so the gate that decides grow-versus-compact is unpinned.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:163:== -Member MakeRoom -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test reading a status line longer than 16384 bytes fails under the mutant.
- Actual: outcome survived: every Curl.Protocol.Http.UnitTests test passed with the mutant applied.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-12: Backlog -> Doing.
