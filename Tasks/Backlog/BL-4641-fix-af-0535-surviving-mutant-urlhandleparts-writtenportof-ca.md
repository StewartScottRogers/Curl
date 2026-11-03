---
id: BL-4641
title: Fix AF-0535: Surviving mutant: UrlHandleParts.WrittenPortOf can drop an explicit port 0 without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
