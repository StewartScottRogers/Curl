---
id: BL-4700
title: Fix AF-0552: BuildsTheDispatcherOverASocketCallbackPool checks the dispatcher only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4700 — Fix AF-0552: BuildsTheDispatcherOverASocketCallbackPool checks the dispatcher only for null

## Goal

The defect the audit office reported as AF-0552 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0552 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0552-buildsthedispatcheroverasocketcallbackpool-checks.md`.

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs:71`

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs:71`

'Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null));' is the only check of the dispatcher; the other assertions check CreateConnectOnlyConnector. A dispatcher built without the SocketCallbackConnector passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs -Pattern 'Assert.IsNotNull\(ManagedCurlEngine.CreateDispatcher'
```

- Expected: An assertion that the dispatcher's pool sits over a SocketCallbackConnector.
- Actual: ManagedEasyTransferTests.Sockets.cs:71: Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(...));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The dispatcher test now drives a transfer through `ManagedCurlEngine.CreateDispatcher` itself (engine built with it as its dispatcher factory) to `http://127.0.0.1:1/` with an open-socket callback answering `CURL_SOCKET_BAD`: it asserts exit 7 with `SocketCallbackConnector`'s text and that the callback was called once with `IpConnection`. A pool without the `SocketCallbackConnector` would dial and never call it. Chose behaviour over reflection because the pool's inner connector is not exposed and `touches` is the test project only; the refusal comes before any socket is made, so it stays a unit test.
- The connect-only connector assertions moved to their own test, `CreateConnectOnlyConnector_SocketCallbackSetOrNot_WrapsTheTcpConnectorOnlyWhenSet`, so each test checks what its name says.
- Test-only change, no production code, so no quality run. Platform-neutral (loopback IP literal, no OS text pinned). Curl.Engine.UnitTests fast tests: 2960 passed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
