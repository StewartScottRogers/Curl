---
id: BL-4886
title: Fix AF-0692: Surviving mutant: any write error (or any header callback that delivered 0) can drop the head from getinfo
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-4886 — Fix AF-0692: Surviving mutant: any write error (or any header callback that delivered 0) can drop the head from getinfo

## Goal

The defect the audit office reported as AF-0692 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0692 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0692-surviving-mutant-any-write-error-or-any-header-cal.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239`

'result.ExitCode.ToCurlCode() == CurlCode.WriteError && lastHeaderCallbackOutput is { BytesDelivered: 0 }' mutated to '||' survived (Engine sample, seed 982898). HeaderCallbackTookNothing decides whether getinfo drops HeaderSize, ContentType and ContentLengthDownload (line 246, CGR-252). With '||', a body write error alone would zero the header size and content type that libcurl reports.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239:&& -Member HeaderCallbackTookNothing -ExcludeBaselineFailures -TimeoutSeconds 600
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
