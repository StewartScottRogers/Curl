---
id: BL-1280
title: Fix AF-0024: The differential tool does not normalise elapsed milliseconds in error text or the -v source port, so identical behaviour is counted as a difference
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-03
completed:
---
# BL-1280 — Fix AF-0024: The differential tool does not normalise elapsed milliseconds in error text or the -v source port, so identical behaviour is counted as a difference

## Goal

The defect the audit office reported as AF-0024 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0024 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0024-the-differential-tool-does-not-normalise-elapsed-m.md`.

Location: `Audit/Tools/Invoke-DifferentialConformance.ps1:230`

Location: `Audit/Tools/Invoke-DifferentialConformance.ps1:230`

Seed 863948043, reference curl 8.21.0 (x86_64-w64-mingw32) Schannel. Cases 95, 116, 159, 187, 223, 282, 284 differ only in 'Failed to connect to 127.0.0.1:PORT after N ms' (for example 2730 ms vs 2724 ms; 0 ms vs 8 ms). Case 254 differs only in '* Established connection ... from 127.0.0.1 port N'. Case 299 differs only in where a progress-meter row lands relative to a warning, which also varies between two runs of real curl. Get-Normalised (line 230) masks the server port, Date, multipart boundary, HAProxy source port and meter lines, but not these values. So 9 of the 43 reported differences (21%) are run-to-run noise, which inflates differentialDifferences.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Audit/Tools/Invoke-DifferentialConformance.ps1 -Pattern 'after \\d\+ ms|port \\d|Established' | Measure-Object | Select-Object -ExpandProperty Count
```

- Expected: At least 1: Get-Normalised masks 'after N ms' and the verbose source port
- Actual: 0

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
