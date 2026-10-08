---
id: BL-1704
title: Fix AF-0061: HttpResponseHeadReader.DefersNoHeader can defer every header (false -> true) with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1704 — Fix AF-0061: HttpResponseHeadReader.DefersNoHeader can defer every header (false -> true) with no test failing

## Goal

The defect the audit office reported as AF-0061 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0061 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0061-httpresponseheadreader-defersnoheader-can-defer-ev.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:56`

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:56`

Mutant: DefersNoHeader = static (_, _) => false; -> => true. It survived the sampled run (seed 0). DefersNoHeader is the default DefersFrom (line 193, documented 'By default no header is deferred'). DefersFrom decides, at line 622, whether a header and every header after it are kept from HeaderReceived and the -v report until they are released. Every head reader built without DefersFrom (only HttpProtocolHandler.cs:1052 sets it) would then defer every acted-on header. No test checks the default reader's header delivery order against that.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:56:false -Member DefersNoHeader -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right; the gap was a missing test. Added
  `HttpResponseHeadReaderTests.ReadAsync_ReaderWithoutDefersFrom_TellsEveryFinalHeaderBeforeReturning`:
  a reader built without `DefersFrom` must have told `HeaderReceived` of every final-head header
  (A, B) by the time `ReadAsync` returns, before `ReportHeldLines`, at 1-, 7- and 65536-byte reads.
- Added `Curl.Protocol.Http.UnitTests` to `touches`: the test belongs there and no task in Doing on
  `origin/work/dark-factory` named it.
- A lane may not read `Audit/` (guard hook), so `Invoke-MutationTest.ps1` could not run here. The same
  mutant was applied by hand (`DefersNoHeader` `false` -> `true`): the new test failed (killed), every
  other `HttpResponseHeadReaderTests` test passed; reverted. The quality auditor's re-audit confirms it.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A test now pins that a head reader without DefersFrom tells HeaderReceived of every header before ReadAsync returns, killing the AF-0061 mutant
