---
id: BL-2588
title: Curl.Engine: finish an active FTP transfer with CURLOPT_FTPPORT - and socket callbacks (test596 hangs)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2588 — Curl.Engine: finish an active FTP transfer with CURLOPT_FTPPORT - and socket callbacks (test596 hangs)

## Goal

An active-mode FTP download through Libcurl (`CURLOPT_FTPPORT` `-`) with open and close socket callbacks finishes, as in test596.

## Context

Found by BL-2527 running test596 through the ported `lib500` (third argument `activeftp`, socket callbacks on for cases 585, 586, 595 and 596): the run does not finish within 20 seconds, while the passive test595 passes. Look at the data connection the engine's `SocketCallbackConnector` wraps for `EPRT` and how the case's in-memory FTP server connects back.

## Acceptance criteria

- [ ] test596 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the fix (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (lane 2, diagnosis, no code changed): the hang is not in `SocketCallbackConnector`. The managed
  engine has no seam for the FTP active-mode listener: `ManagedCurlEngine.CreateDispatcher` never passes
  `ftpListener` to `EngineComposition.CreateProtocolHandlers`, so `CreateFtpProtocolHandler` falls back to a real
  `TcpConnectionListener`, while the case's in-memory FTP server connects back through
  `FtpServerConnector.ActiveModeListener` (already on `UpstreamCurlInvocation.ConnectionListener`, set by
  `UpstreamCaseRunner` for lib cases too). Nobody accepts, so the transfer waits forever.
- Plan: add an `IConnectionListener? FtpListener` (default null) to `EngineDialing` and pass it as `ftpListener` in
  `CreateDispatcher`; in `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` `CreateEngine`, pass
  `invocation.ConnectionListener`. Then the socket callbacks: test596's datacheck is `[OPEN] 1` (control),
  `[OPEN] 2` (the listening socket - curl 8.22.0's `ftp_state_use_port` opens it through `Curl_socket_open`, so the
  open-socket callback sees it), `[CLOSE] 2` (curl closes the listening socket with the callback once it accepts;
  the accepted socket is closed without it, `cf_socket_close` passes `!ctx->accepted`), `moo`, `[CLOSE] 1`. So wrap
  the listener with the socket callbacks like `SocketCallbackConnector`: open-socket at listen, close-socket at
  accept, nothing for the accepted connection.
- Touches widened: `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` (the lib engine's composition) and
  `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (criterion 1). The ratchet file is in BL-2528's `touches`
  (Doing), so the task goes back to Backlog until BL-2528 lets it go.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 9f887a5f 904b57ec 3d836d5e.
- 2026-10-17: Doing -> Backlog. Needs Curl.Conformance.UnitTests/PassingUpstreamCases.txt, which BL-2528 (Doing) touches; diagnosis and plan are in Notes
- 2026-10-17: Backlog -> Doing. Criteria at claim: 9f887a5f 904b57ec 3d836d5e.
