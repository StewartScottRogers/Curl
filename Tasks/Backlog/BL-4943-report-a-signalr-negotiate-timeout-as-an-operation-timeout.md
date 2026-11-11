---
id: BL-4943
title: Report a SignalR negotiate timeout as an operation timeout
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs, Curl.Extended.Protocol.SignalR.UnitTests/SignalRNegotiateTimeoutTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4943 — Report a SignalR negotiate timeout as an operation timeout

## Goal

A `-m` timeout during negotiate prints `Operation timed out after ... with 0 bytes received`, as http, socketio and nats do.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-021 (Low, confirmed: reproduced at least twice).

- Repro: TCP server on 18608 that accepts and never replies. `curl -sS -m 2 -d "{\"target\":\"A\"}" signalr://127.0.0.1:18608/hub`
- Expected: `curl: (28) Operation timed out after 2000 milliseconds with 0 bytes received`.
- Actual: `curl: (28) Connection timed out after 2010 milliseconds`.
- Where to start: The negotiate step is timed as part of connecting in `SignalRProtocolHandler.cs`.

## Acceptance criteria

- [ ] The repro prints `curl: (28) Operation timed out after 2000 milliseconds with 0 bytes received` (time from the injected `TimeProvider` in the unit test).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
