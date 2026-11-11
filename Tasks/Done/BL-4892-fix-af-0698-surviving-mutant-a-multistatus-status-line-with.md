---
id: BL-4892
title: Fix AF-0698: Surviving mutant: a multistatus status line with no reason phrase can be refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebDavTools.UnitLibrary, Curl.Extension.WebDavTools.UnitTests/Multistatus/WebDavMultistatusReaderTests.cs]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4892 — Fix AF-0698: Surviving mutant: a multistatus status line with no reason phrase can be refused

## Goal

The defect the audit office reported as AF-0698 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0698 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0698-surviving-mutant-a-multistatus-status-line-with-no.md`.

Location: `Curl.Extension.WebDavTools.UnitLibrary/Multistatus/WebDavMultistatusReader.cs:101`

Location: `Curl.Extension.WebDavTools.UnitLibrary/Multistatus/WebDavMultistatusReader.cs:101`

'if (parts.Length < 2' mutated to '<= 2' survived. A valid status like 'HTTP/1.1 200' (no reason phrase) splits into 2 parts and would throw InvalidStatus. Every test status carries a reason phrase, and the refused cases ('HTTP/1.1', 'FTP/1.1 200 OK', ...) do not cover two parts.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.WebDavTools.UnitLibrary/Multistatus/WebDavMultistatusReader.cs:101:< -Member ParseStatus -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Read_StatusLineWithoutReasonPhrase_GivesItsCode` to `WebDavMultistatusReaderTests`: `HTTP/1.1 404` (two parts, no reason phrase) must read as 404, so the `parts.Length <= 2` mutant at line 101 now throws InvalidStatus and fails it. No production change was needed. Widened `touches` to that test file (no other task in Doing names it). The audit tool under `Audit/` is off limits to a lane, so the reproduction was checked by reasoning about the mutant against the new test; the quality re-audit confirms it.
- `dotnet build` clean (0 warnings) and the fast tests green on Windows; the test is platform-neutral (no paths, OS errors or certificates).

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. A multistatus status line without a reason phrase is pinned to read as its code, killing AF-0698's mutant
