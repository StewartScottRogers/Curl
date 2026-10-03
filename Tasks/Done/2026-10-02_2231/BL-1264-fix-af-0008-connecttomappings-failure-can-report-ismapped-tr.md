---
id: BL-1264
title: Fix AF-0008: ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1264 — Fix AF-0008: ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing

## Goal

The defect the audit office reported as AF-0008 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0008 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0008-connecttomappings-failure-can-report-ismapped-true.md`.

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:135`

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:135`

Mutant survived (seed 0): `new(string.Empty, 0, IsMapped: false, message)` became `IsMapped: true`. A failed --connect-to parse could then claim a mapping, and no test checks IsMapped on a failure result.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at Curl.Networking.UnitLibrary/ConnectToMappings.cs:135 (false) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/ConnectToMappings.cs:135 false

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The fix is test-only: `Map_WhenTheMatchingDestinationDoesNotParse_ReportsCurlsExit49Message` now asserts the whole failure `ConnectDestination` (empty host, port 0, `IsMapped: false`, the message) rather than only `ParseError`. The production code was already right.
- Added `Curl.Networking.UnitTests` to `touches`: the tests live there. No other task in Doing on `origin/work/dark-factory` names it.
- The audit guard refuses lanes access to `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was run by hand: the mutant (`IsMapped: true` at `ConnectToMappings.cs:135`) applied with the Edit tool failed 9 of 42 `ConnectToMappings` tests, then the code was reverted. The quality auditor's re-audit still has to confirm it with the tool itself.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: all 33 test projects passed.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Tests now pin IsMapped: false on every --connect-to parse failure; the AF-0008 mutant is killed
