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
completed: 2026-11-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `AltSvcTransferCacheTests.ApplyTo_EntryForAnotherHostLearnedOverHttp2_SwitchesVersionOnlyWhenTheAlpnDiffers`: an entry learned over HTTP/2 for another host keeps `--http2`'s HTTP/2 for `h2` and switches `--http3` to HTTP/3 alone for `h3` (under `--http2` an h3 entry is not looked up, so that row uses `--http3`). Production code unchanged, so no quality run.
- A lane cannot run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`==` to `!=` on line 288): both rows fail under it and pass without it; the line was restored. The re-audit runs the tool itself.
- `dotnet build` clean; fast tests green (no failing test project).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A test now kills AltSvcTransferCache.SwitchedVersion's == to != mutant (AF-0561)
