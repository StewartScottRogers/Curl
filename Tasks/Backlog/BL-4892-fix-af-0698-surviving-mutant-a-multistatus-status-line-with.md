---
id: BL-4892
title: Fix AF-0698: Surviving mutant: a multistatus status line with no reason phrase can be refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebDavTools.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
