---
id: BL-4642
title: Fix AF-0536: Surviving mutant: AltSvcTransferCache.SameDestinationVersion can invert which alt-svc entries race HTTP/3 without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-02
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Narrowed `touches` from the whole Curl.Engine.UnitLibrary to the production file and its test file; the fix is a test, so only `Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs` changed (no task in Doing on origin/work/dark-factory names Curl.Engine.UnitTests). The narrowing was not pushed: the lane prompt forbids pushing.
- Added `ApplyTo_EntryForTheSameHostAndPortWithNoVersionOption_PrefersHttp3OnlyForAnH3Entry` (h3 entry gives Http3, h2 and h1 keep Http11) and `ApplyTo_H2EntryForTheSameHostAndPortUnderHttp3_KeepsHttp3`.
- Reproduction: lanes may not run `Audit/Tools`, so the mutant was applied by hand (`==` to `!=` on line 268): 3 of the 15 AltSvcTransferCacheTests then fail, so it is killed; reverted. Production code unchanged, so no Measure-CodeQuality run was needed.

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-02: Doing -> Done. Tests now kill AF-0536's mutant: only an h3 entry for the origin races HTTP/3 without a version option
