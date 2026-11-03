---
id: BL-4642
title: Fix AF-0536: Surviving mutant: AltSvcTransferCache.SameDestinationVersion can invert which alt-svc entries race HTTP/3 without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4642 — Fix AF-0536: Surviving mutant: AltSvcTransferCache.SameDestinationVersion can invert which alt-svc entries race HTTP/3 without a test failing

## Goal

The defect the audit office reported as AF-0536 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0536 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0536-surviving-mutant-altsvctransfercache-samedestinati.md`.

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:268`

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:268`

Mutant 'destination == AltSvcAlpn.H3 && requestedVersion == HttpVersion.None ? Http3 : version' -> 'destination != AltSvcAlpn.H3 ...' survived (Curl.Engine.UnitTests green). With it, an alt-svc entry naming h3 for the origin no longer races HTTP/3 against TCP (as the doc comment says curl was measured to do), while h2 and h1 entries start a QUIC attempt. The protocol and connections a transfer uses change, with no test noticing.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:268:== -Member SameDestinationVersion -ExcludeBaselineFailures -TimeoutSeconds 600
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
