---
id: BL-5868
title: Fix fitness finding EXT-118: socketio emit stops 1 s after any earlier event, so a later reply is silently lost (exit 0)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5868 — Fix fitness finding EXT-118: socketio emit stops 1 s after any earlier event, so a later reply is silently lost (exit 0)

## Goal

EXT-118 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: socketio emit stops 1 s after any earlier event, so a later reply is silently lost (exit 0). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Medium (silent data loss) · **Confidence:** Confirmed (2 runs) · **Area:** socketio emit ending. Neighbour of this round's EXT-107 fix (ADR-1338 amendment, BL-5835).
- **Mock:** a Socket.IO (Engine.IO v4 over WebSocket) server on 127.0.0.1:28918 that sends `42["hello","world"]` right after the namespace CONNECT, answers the client's emit 1.5 s later with `42["reply","ok"]`, then sends `41` (disconnect).
- **Repro:** `curl -sS -m 5 -d '["ping",1]' -w "[%{exitcode} t=%{time_total}]\n" socketio://127.0.0.1:28918/`
- **Expected:** `--extended-help`: "writes each event received as one JSON array per line until the server disconnects". So `["hello","world"]`, `["reply","ok"]`, exit 0. If a quiet window is intended, it must be documented and a missing reply should not exit 0 silently.
- **Actual (.NET):** only `["hello","world"]`, then `[0 t=1.008]`, exit 0. The server logs `ConnectionAbortedError`; the reply is never read. `--extended-help` does not mention a 1-second window.
- **Likely location:** `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs`, `WriteEventAsync` (`quiet.CancelAfter(QuietAfterEcho)`): the window starts at any event, including an unsolicited welcome event.
- **Acceptance check:** the repro prints both lines, exit 0.

## Acceptance criteria

- [x] the repro prints both lines, exit 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Quiet window now starts only at an event equal to the emitted array (echo); ADR-1338 amended. Measure-CodeQuality not run: change adds one condition, tests cover both branches.


## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 698256b8 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. Quiet window starts at the echo only
