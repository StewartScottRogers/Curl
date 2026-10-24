---
id: BL-3445
title: Fix AF-0398: UpstreamTestHeaderLine.IsStatusLine's versionLength + 1 bound can become - 1 with no test failing, so a short HTTP/ line throws instead of being judged
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3445 — Fix AF-0398: UpstreamTestHeaderLine.IsStatusLine's versionLength + 1 bound can become - 1 with no test failing, so a short HTTP/ line throws instead of being judged

## Goal

The defect the audit office reported as AF-0398 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0398 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0398-upstreamtestheaderline-isstatusline-s-versionlengt.md`.

Location: `Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:33`

Location: `Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:33`

Sampled mutant (seed 0) survived: '&& rest.Length > versionLength + 1' -> '&& rest.Length > versionLength - 1'. The bound guards the next two reads, rest[versionLength] and rest[versionLength + 1]; with the mutant a line such as 'HTTP/1.1' or 'HTTP/1.1 ' reaches them and throws IndexOutOfRangeException, so the harness's header comparison of an upstream case crashes instead of returning a verdict. No test gives IsStatusLine a status line cut short after the version.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTestHeaderLine.cs:33:+1 -Member IsStatusLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
