---
id: BL-2638
title: Fix upstream case 766: active FTP calls the socket-option callback for the accepted data connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2638 — Fix upstream case 766: active FTP calls the socket-option callback for the accepted data connection

## Goal

Upstream case 766 passes through the runner and holds the ratchet.

## Context

Found by BL-2536, which ported lib766 (`Curl.Conformance.LibTest.UnitLibrary/Lib766.cs`) and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. The program runs an active FTP transfer (`CURLOPT_FTPPORT` `-`) whose `CURLOPT_SOCKOPTFUNCTION` returns `CURL_SOCKOPT_ERROR` for `CURLSOCKTYPE_ACCEPT`. Upstream calls the callback when the data connection is accepted, right after `EPRT`, and fails with 42 (`CURLE_ABORTED_BY_CALLBACK`). The runner reports: `test766 fails: <verify><protocol> differs at byte 63 (line 6): expected the end, got "SIZE 766\r\n"`, so Curl's active FTP path never calls the socket-option callback with `Accept` (ADR-0610 has the engine calling it for each TCP connection it opens). Likely in `Curl.Protocol.Ftp.UnitLibrary`'s active data connection or the engine's socket-callback wiring; find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] test766 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it still fails it is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: da7e849c c59fcab3 3d836d5e.
