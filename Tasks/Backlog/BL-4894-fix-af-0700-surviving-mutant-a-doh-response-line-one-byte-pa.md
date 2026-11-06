---
id: BL-4894
title: Fix AF-0700: Surviving mutant: a DoH response line one byte past the limit can be read
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-4894 — Fix AF-0700: Surviving mutant: a DoH response line one byte past the limit can be read

## Goal

The defect the audit office reported as AF-0700 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0700 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0700-surviving-mutant-a-doh-response-line-one-byte-past.md`.

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:219`

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:219`

'while (next is >= 0 and not '\n' && line.Count < MaximumLineLength)' mutated to '<= MaximumLineLength' survived. MaximumLineLength is 100 * 1024. The method should return null for a line that runs past it (comment at line 212), but no test sends a line at the boundary, so the limit can be off by one.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohResponseReader.cs:219:< -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
