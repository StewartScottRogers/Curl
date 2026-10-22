---
id: BL-2798
title: Fix AF-0271: EngineComposition's HTTP/3-only second HTTPS-connect attempt can apply to every version with no Engine test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2798 — Fix AF-0271: EngineComposition's HTTP/3-only second HTTPS-connect attempt can apply to every version with no Engine test failing

## Goal

The defect the audit office reported as AF-0271 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0271 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0271-enginecomposition-s-http-3-only-second-https-conne.md`.

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:1046`

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:1046`

Mutant HttpsConnectSecondAttemptVersion = http.HttpVersion == HttpVersion.Http3 ? "h2" : null -> != survived in Curl.Engine.UnitTests (sampled run, seed 0). With it every non-HTTP/3 HTTPS connect is given an h2 second attempt for the [HTTPS-CONNECT] filter, and HTTP/3 gets none. That is a connect decision whose trace lines reach -v output. Only Curl.Console.UnitTests (CurlCommandRunnerHttpsConnectTraceTests:156) pins the value; the library's own twin does not.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:1046:== -Member ConnectToMappings -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Engine.UnitTests` to `touches`: the fix is a test in the library's own twin, and no other task in Doing on `origin/work/dark-factory` names it.
- Fix: `Curl.Engine.UnitTests/EngineCompositionHttpsConnectAttemptTests.cs` pins `CreateTcpConnector`'s `HttpsConnectSecondAttemptVersion`: `h2` under `HttpVersion.Http3`, `null` under `Http3Only`, `Http2`, `Http11` and `Http10` (curl.se's 8.22.0 ngtcp2 build, BL-1284 Notes). No production change.
- Reproduction: the audit guard refuses a lane `Audit/Tools/Invoke-MutationTest.ps1`, so the same mutant (`==` -> `!=` at the `HttpsConnectSecondAttemptVersion` line, now line 1053) was applied by hand: all 5 new tests failed (killed), then the line was restored. The quality auditor's re-audit confirms with the tool itself.
- Build clean; fast tests green (Curl.Engine.UnitTests 2138 passed, 5 skipped; no project failed).

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. EngineComposition's HTTP/3-only h2 second attempt is pinned by Curl.Engine.UnitTests; the AF-0271 mutant is killed
