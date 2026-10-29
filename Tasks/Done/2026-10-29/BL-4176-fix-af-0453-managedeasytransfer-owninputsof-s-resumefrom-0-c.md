---
id: BL-4176
title: Fix AF-0453: ManagedEasyTransfer.OwnInputsOf's ResumeFrom > 0 can become >= 0 with no test failing, so a zero resume offset is passed on as a resume
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolInputs.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4176 — Fix AF-0453: ManagedEasyTransfer.OwnInputsOf's ResumeFrom > 0 can become >= 0 with no test failing, so a zero resume offset is passed on as a resume

## Goal

The defect the audit office reported as AF-0453 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0453 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0453-managedeasytransfer-owninputsof-s-resumefrom-0-can.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:150`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:150`

Line 150: `ResumeFrom: options.Transfer.ResumeFrom > 0 ? options.Transfer.ResumeFrom : null,`. Invoke-MutationTest.ps1 -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0 changed `>` to `>=` and every Curl.Engine.UnitTests test passed (survived). The change matters because consumers tell null from 0. Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs:1358 `if (context.ResumeFrom is { } resumeFrom)` takes the resume path, which wins over CURLOPT_RANGE. FileProtocolHandler.cs:680 `context.ResumeFrom is null && context.Range is { } range` ignores the range for an unknown-size source. So under the mutant, a libcurl easy handle with a range on a file:// URL and the default RESUME_FROM of 0 delivers the whole file instead of the range: different output bytes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:150:> -Member OwnInputsOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test sees a zero resume offset passed on as a resume.
- Actual: outcome survived: every Curl.Engine.UnitTests test passed with the mutant.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production line is right (a zero CURLOPT_RESUME_FROM is no resume); what was missing was a test. Added `PerformAsync_NoToolInputsAndResumeFromZero_PassesNoResumeOffset` to Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolInputs.cs, which was added to touches (no task in Doing on origin/work/dark-factory names it).
- Reproduction: the audit guard refuses a lane running the audit office's mutation tool, so the same mutant (`>` to `>=` at ManagedEasyTransfer.SetUp.cs:150) was applied by hand and the new test failed under it (killed); the source was then restored. The re-audit confirms with the tool itself.
- No production code changed, so no Measure-CodeQuality run. Build clean, fast tests 0 failed on Windows; the test is platform-neutral (an http URL and a recording handler), which meets CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-29: Doing -> Done. A test now kills the ResumeFrom > 0 to >= 0 mutant in OwnInputsOf: a zero resume offset reaches the handler as no resume
