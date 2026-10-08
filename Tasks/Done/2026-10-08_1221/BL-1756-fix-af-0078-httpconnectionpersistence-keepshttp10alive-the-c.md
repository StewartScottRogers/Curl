---
id: BL-1756
title: Fix AF-0078: HttpConnectionPersistence.KeepsHttp10Alive: the close check's slice 'colon + 1' can become 'colon - 1' with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1756 — Fix AF-0078: HttpConnectionPersistence.KeepsHttp10Alive: the close check's slice 'colon + 1' can become 'colon - 1' with no test failing

## Goal

The defect the audit office reported as AF-0078 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0078 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0078-httpconnectionpersistence-keepshttp10alive-the-clo.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:89`

Location: `Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:89`

Sampled mutant (seed 0) survived: '&& !NamesOption(headerLine[(colon + 1)..], "close");' -> '&& !NamesOption(headerLine[(colon - 1)..], "close");'. NamesOption splits on ',' and trims, so the mutant reads the first token as 'n: close' and misses a close given first. For the HTTP/1.0 header 'Connection: close, keep-alive', curl's rule (keep-alive named and close not named) gives false, but the mutant gives true. HttpResponseHeadReader.cs:513 would then print the HTTP/1.0 keep-alive info line and keep a connection the server said to close. KeepsHttp10Alive_HeaderLine_IsTrueForAConnectionHeaderNamingKeepAliveAndNotClose (HttpConnectionPersistenceTests.cs:110-119) has 'keep-alive, close' but never 'close, keep-alive', so no row catches it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:89:+1 -Member KeepsHttp10Alive -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: a test row only. `KeepsHttp10Alive_HeaderLine_IsTrueForAConnectionHeaderNamingKeepAliveAndNotClose`
  gains `Connection: close, keep-alive` -> false ("close wins when named first"). The
  production code was already right; the mutant reads the first token as `n: close` and
  so answers true, which the new row fails.
- `touches` gained `Curl.Protocol.Http.UnitTests`: the test lives there. No task in Doing
  on `origin/work/dark-factory` names it (only BL-1730, Gap tools).
- Verification: the lane's audit guard refuses `Audit/Tools/Invoke-MutationTest.ps1`, so
  the mutant was applied by hand (`colon + 1` -> `colon - 1` at line 89): the new row
  failed (1 of 16), the others passed - killed. Reverted; all 16 pass. The audit office's
  re-audit runs the tool itself.
- Library code is unchanged, so its coverage is unchanged; no Measure-CodeQuality run.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. A test row with close named first kills the AF-0078 mutant in KeepsHttp10Alive
