---
id: BL-2570
title: Fix AF-0233: After giving up on refused-stream retries the connection can be kept alive with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2570 — Fix AF-0233: After giving up on refused-stream retries the connection can be kept alive with no test failing

## Goal

The defect the audit office reported as AF-0233 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0233 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0233-after-giving-up-on-refused-stream-retries-the-conn.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975`

Mutant 'new HttpAttemptOutcome(refused with { ErrorMessage = ConnectionDiedGivingUp(...) }, null, KeepsAlive: false)' -> 'KeepsAlive: true' survived. A connection the peer kept refusing would go back to the pool, and the next URL of the same run could reuse it. No test checks the pool state after the 'Connection died, tried N times before giving up' outcome.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975:false -Member StreamRefusedOutcome -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was already right (`KeepsAlive: false`); the gap was a missing test. Added
  `ExecuteAsync_Http3StreamRefusedEveryTime_ClosesTheLastConnectionRatherThanLeavingItIntact` in
  `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3.cs`: after the sixth refusal `-v` must end
  with `closing connection #N` and never `left intact` (a kept-alive outcome marks the connection reusable
  and reports it left intact).
- Added `Curl.Protocol.Http.UnitTests` to `touches`: the fix is a test; no task in Doing on
  `origin/work/dark-factory` names it.
- The audit guard refuses a lane running `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied
  by hand (line 1975 `KeepsAlive: false` -> `true`): the new test failed against it (killed), and passed
  once reverted. The re-audit runs the tool itself.
- No production code changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-17: Doing -> Done. A test now fails if the connection is kept alive after giving up on refused-stream retries (AF-0233 mutant killed)
