---
id: BL-4709
title: Fix AF-0561: Surviving mutant: AltSvcTransferCache.SwitchedVersion can invert which HTTP version an alt-svc switch uses without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4709 — Fix AF-0561: Surviving mutant: AltSvcTransferCache.SwitchedVersion can invert which HTTP version an alt-svc switch uses without a test failing

## Goal

The defect the audit office reported as AF-0561 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0561 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0561-surviving-mutant-altsvctransfercache-switchedversi.md`.

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:288`

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:288`

Mutant 'match.SourceAlpn == match.Entry.DestinationAlpn' -> '!=' survived. The mutant keeps the requested version when the destination ALPN differs, and forces HTTP/3-only or HTTP/1.1 when it is the same, so an alt-svc h2->h3 switch would not use HTTP/3 and the request bytes and protocol change.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:288:== -Member SwitchedVersion -ExcludeBaselineFailures -TimeoutSeconds 600
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
