---
id: BL-2571
title: Fix AF-0234: A timed-out HTTP transfer's connection can be marked keep-alive with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2571 — Fix AF-0234: A timed-out HTTP transfer's connection can be marked keep-alive with no test failing

## Goal

The defect the audit office reported as AF-0234 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0234 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0234-a-timed-out-http-transfer-s-connection-can-be-mark.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1348`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1348`

Mutant 'return new HttpAttemptOutcome(timedOut, null, KeepsAlive: false);' -> 'KeepsAlive: true' survived. A connection abandoned mid-response by -m/exit 28 would be pooled, and a later URL in the same run could read the rest of the old response as its own. The timeout tests check the exit-28 message, body and report, but not that the connection is closed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1348:false -Member OutcomeOfTimeout -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `ExecuteAsync_ServerStallsPastMaxTime_ClosesTheConnectionRatherThanLeavingItIntact` (HttpProtocolHandlerTests.Timeouts.cs): after exit 28 the `-v` lines carry no "left intact" and the last connection line is "closing connection #". Lanes may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit guard), so the mutant was applied by hand (`KeepsAlive: true` at HttpProtocolHandler.cs:1348): the new test failed, the other 6 StallsPastMaxTime tests passed; mutant reverted. Test-only change, so no production quality run was needed.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-17: Doing -> Done. Test kills the AF-0234 timeout keep-alive mutant
