---
id: BL-4794
title: Fix AF-0621: Surviving mutant: CurlSocketCloser's default can mark every socket as the connection's own without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4794 — Fix AF-0621: Surviving mutant: CurlSocketCloser's default can mark every socket as the connection's own without a test failing

## Goal

The defect the audit office reported as AF-0621 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0621 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0621-surviving-mutant-curlsocketcloser-s-default-can-ma.md`.

Location: `Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19`

Location: `Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19`

Mutant 'bool isTheConnectionsOwnSocket = false' -> '= true' survived. Two callers rely on the default: SocketCallbackConnectionListener.cs:63 (an accepted socket with the close-socket callback) and SocketCallbackConnector.cs:116. With the mutant, IsTheConnectionsOwnSocket is true for them, so the close-socket callback runs before the connection closes rather than after (CGR-129). That changes the callback order libcurl callers observe; no test checks it for these two paths.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19:false -Member CurlSocketCloser -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `CurlSocketCloserTests` (the default `isTheConnectionsOwnSocket` is false, the flag given is true, `Close` runs the callback once then disposes the owned socket) and `SocketCallbackConnectorTests.DisposeAsync_CloserNotMarkedAsTheConnectionsOwnSocket_CallsTheCloseSocketCallbackAfterTheConnectionCloses`, which pins the callback order the two default-relying callers give (CGR-129).
- Reproduction: `Audit/Tools` is closed to lanes, so the mutant was applied by hand (`= false` -> `= true` at CurlSocketCloser.cs:19): both new tests failed against it (2 failed of 2977), and all 2970 passed once it was reverted. That is the killed result the finding expects.
- Only test code changed (Curl.Engine.UnitTests), so no production member moved and no quality run was needed. The tests are platform-neutral (no paths, sockets or OS text), so CI on Linux and macOS should match.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Mutant killed by new CurlSocketCloser and SocketCallbackConnection dispose-order tests
