---
id: BL-1702
title: Fix AF-0059: DohResponseReader.ReadLineAsync: 'next is >= 0' can become '> 0' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1702 — Fix AF-0059: DohResponseReader.ReadLineAsync: 'next is >= 0' can become '> 0' with no test failing

## Goal

The defect the audit office reported as AF-0059 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0059 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0059-dohresponsereader-readlineasync-next-is-0-can-beco.md`.

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:182`

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:182`

Mutant: while (next is >= 0 and not '\n' && ...) -> while (next is > 0 and not '\n' && ...). It survived the sampled run (seed 0). With the mutant, a NUL byte in a DoH response's status or header line stops the line early and ReadLineAsync returns null. The DoH lookup then fails instead of reading the head, which changes a refused input. No test sends a head line containing a 0x00 byte.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohResponseReader.cs:182:>= -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Networking.UnitTests` to `touches`: the fix is a test, and no other task in Doing on `origin/work/dark-factory` names that project.
- No production change. The test `ReadBodyAsync_WithANulByteInAChunkExtension_ReadsTheWholeSizeLine` sends a chunk size line `7;name=a\0b`: curl's chunk parser skips every byte after the size up to the LF, so the body is read. Chose a chunk extension rather than a status or header line because curl rejects a NUL byte in a header line ("Nul byte in header"), so pinning a header line with a NUL as accepted would pin a divergence; the chunk extension is where today's behaviour and curl's agree.
- Lanes may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit guard). Applied the mutant (`next is > 0`) by hand instead: the new test failed (1 of 31 DoH reader tests), so the mutant is killed; then reverted it. The quality auditor's re-audit confirms with the tool itself.
- Follow-up worth considering: curl fails a DoH response whose status or header line holds a NUL byte; `DohResponseReader` accepts it. Not changed here (outside this finding).

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Test kills the AF-0059 mutant; build and fast tests green
