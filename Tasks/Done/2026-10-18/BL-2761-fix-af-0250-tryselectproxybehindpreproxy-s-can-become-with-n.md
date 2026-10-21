---
id: BL-2761
title: Fix AF-0250: TrySelectProxyBehindPreProxy's && can become || with no test failing, refusing every HTTP proxy behind a pre-proxy with exit 5
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2761 — Fix AF-0250: TrySelectProxyBehindPreProxy's && can become || with no test failing, refusing every HTTP proxy behind a pre-proxy with exit 5

## Goal

The defect the audit office reported as AF-0250 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0250 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0250-tryselectproxybehindpreproxy-s-can-become-with-no.md`.

Location: `Curl.Engine.UnitLibrary/TransferProxySelection.cs:184`

Location: `Curl.Engine.UnitLibrary/TransferProxySelection.cs:184`

Engine sample, seed 0: survived. Original `if (selected is not null && !IsHttpKind(selected.Kind))`, mutated `||`. With the mutant, any selected proxy, including an HTTP one, fails with CouldntResolveProxy "Having a SOCKS pre-proxy and proxy is not supported". A null selection would throw a NullReferenceException. No test runs a pre-proxy with an accepted HTTP proxy through this method, so a changed exit code (5) would reach users.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferProxySelection.cs:184:&& -Member TrySelectProxyBehindPreProxy -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right; the gap was a missing test. Added
  `EasyProxyOptionsReadingTests.TrySelect_SocksPreProxyAndAnHttpProxy_IsTheHttpProxy` (an HTTP
  `-x` behind a SOCKS pre-proxy is selected, no failure) and
  `TrySelect_SocksPreProxyAndASocksProxy_IsExit5` (a SOCKS `-x` behind one is exit 5).
- Added `Curl.Engine.UnitTests` to `touches`: the test belongs beside the engine's other
  record-driven proxy tests, and no task in Doing on `origin/work/dark-factory` names it.
- The audit guard refuses lanes `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was run by
  hand: line 184's `&&` changed to `||`, `Curl.Engine.UnitTests` built with
  `-p:TreatWarningsAsErrors=false -p:Nullable=disable` (the mutant does not compile otherwise) and
  its fast tests run: `TrySelect_SocksPreProxyAndAnHttpProxy_IsTheHttpProxy` failed, so the mutant
  is killed; the line was then restored. The quality auditor's re-audit runs the tool itself.
- No production code changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. A test now pins an HTTP proxy behind a SOCKS pre-proxy, killing AF-0250's && to || mutant
