---
id: BL-1702
title: Fix AF-0059: DohResponseReader.ReadLineAsync: 'next is >= 0' can become '> 0' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
