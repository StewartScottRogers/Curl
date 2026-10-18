---
id: BL-2638
title: Fix upstream case 766: active FTP calls the socket-option callback for the accepted data connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2638 — Fix upstream case 766: active FTP calls the socket-option callback for the accepted data connection

## Goal

Upstream case 766 passes through the runner and holds the ratchet.

## Context

Found by BL-2536, which ported lib766 (`Curl.Conformance.LibTest.UnitLibrary/Lib766.cs`) and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. The program runs an active FTP transfer (`CURLOPT_FTPPORT` `-`) whose `CURLOPT_SOCKOPTFUNCTION` returns `CURL_SOCKOPT_ERROR` for `CURLSOCKTYPE_ACCEPT`. Upstream calls the callback when the data connection is accepted, right after `EPRT`, and fails with 42 (`CURLE_ABORTED_BY_CALLBACK`). The runner reports: `test766 fails: <verify><protocol> differs at byte 63 (line 6): expected the end, got "SIZE 766\r\n"`, so Curl's active FTP path never calls the socket-option callback with `Accept` (ADR-0610 has the engine calling it for each TCP connection it opens). Likely in `Curl.Protocol.Ftp.UnitLibrary`'s active data connection or the engine's socket-callback wiring; find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] test766 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it still fails it is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: Cause, two parts. (1) `Curl.Engine`'s `SocketCallbackConnectionListener` wrapped FTP's active-mode listener only for the open- and close-socket callbacks and never asked `CURLOPT_SOCKOPTFUNCTION` about the accepted connection; curl 8.22.0's `cf_tcp_accept_connect` calls it with `CURLSOCKTYPE_ACCEPT` and fails with 42. (2) Curl accepted the data connection only after `RETR`'s 150, but curl's `ftp_do_more` connects the secondary socket right after `EPRT`/`PORT` is answered, and ftpserver.pl connects while answering `EPRT`, so curl accepts (and fails) before `SIZE`, sending no `QUIT` (`ftp_done` marks the control connection invalid for 42).
- Fix: `IPendingConnection.HasConnectionWaiting` (default `false`; `TcpPendingConnection` polls the listening socket with `Poll(0, SelectRead)` as curl's `SOCKET_READABLE(sock, 0)`; the conformance `FtpActiveModeListener` reports a connection already handed over). `FtpSession` accepts at once after an answered `EPRT`/`PORT` when one is waiting (`AcceptWaitingConnectionAsync`), keeping the accepted connection for the usual accept step after the transfer command, so the `-v` lines and command order of a successful transfer do not change; a failed early accept ends the transfer with its exit code and no `QUIT`. The engine's listener wrapper now also wraps for a socket-option callback alone and asks it with `CurlSocketType.Accept` after the accept (the accepted socket numbered by `CurlSocketHandles`), `Error` closing it and failing with exit 42 and `Operation was aborted by an application callback`.
- Touches widened to the five libraries and their tests above: the fix needs the Abstractions contract, its TCP and in-memory implementations, the engine wrapper and the FTP session. No other task was in Doing on `origin/work/dark-factory` when checked, so nothing overlaps.
- Focused unit tests (ADR-0482): `SocketCallbackConnectionListenerTests` (Accept refusal, Accept OK, accept failure, no socket-option callback), `FtpProtocolHandlerActiveModeTests` (server connected at EPRT: failed accept ends with no QUIT; successful one retrieves), `TcpPendingConnectionTests.HasConnectionWaiting_NothingConnected_IsFalse`, `IPendingConnectionTests`.
- Platform check: the case runs on the in-memory FTP stand-in and a managed callback, no path, OS error or certificate, so its output does not depend on the platform.
- Measured: fast tests green (42 test assemblies); `Measure-CodeQuality.ps1` for the five libraries left one branch in `CallbackPendingConnection.AcceptAsync` uncovered, closed by a test, and a rerun for `Curl.Engine.UnitLibrary` reports 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: da7e849c c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. test766 passes: active FTP accepts a server already connected after EPRT and asks the socket-option callback with CURLSOCKTYPE_ACCEPT, failing with 42 and no QUIT
