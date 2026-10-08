---
id: BL-1704
title: Fix AF-0061: HttpResponseHeadReader.DefersNoHeader can defer every header (false -> true) with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
