---
id: BL-4641
title: Fix AF-0535: Surviving mutant: UrlHandleParts.WrittenPortOf can drop an explicit port 0 without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-02
---
# BL-4641 — Fix AF-0535: Surviving mutant: UrlHandleParts.WrittenPortOf can drop an explicit port 0 without a test failing

## Goal

The defect the audit office reported as AF-0535 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0535 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0535-surviving-mutant-urlhandleparts-writtenportof-can.md`.

Location: `Curl.Engine.UnitLibrary/UrlHandleParts.cs:132`

Location: `Curl.Engine.UnitLibrary/UrlHandleParts.cs:132`

Mutant 'if (url.Port < 0)' -> 'if (url.Port <= 0)' survived (Curl.Engine.UnitTests green). A URL written with port 0 (http://h:0/) would report no stored port, so curl_url_get(CURLUPART_PORT) answers CURLUE_NO_PORT and the rebuilt URL loses ':0' where upstream keeps the written port. No test covers port 0 in the URL handle.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlHandleParts.cs:132:< -Member WrittenPortOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `ManagedCurlUrlHandleTests.Get_PortZeroWritten_KeepsItInThePortAndTheUrl`: `http://h:0/` answers port `0` and keeps `:0` in the rebuilt URL. No production change: `WrittenPortOf` was already right, only the test was missing.
- The audit reproduction script lives under `Audit/`, which a lane may not run, so the mutant was checked by hand. With `UrlHandleParts.cs:132` changed to `url.Port <= 0`, the new test fails (`Get` answers `NoPort`, not `Ok`). With the line put back it passes, so the mutant is now killed.
- `touches` widened to the one test file in `Curl.Engine.UnitTests`. The test belongs there, and no task in Doing on origin/work/dark-factory names that project.
- No Measure-CodeQuality run: no production code changed. `dotnet build` is clean (0 warnings, 0 errors) and all 115 fast test assemblies passed on Windows. The test is platform-neutral (no path or OS text).

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-02: Doing -> Done. UrlHandleParts.WrittenPortOf's port-0 mutant is killed by a new URL handle test
