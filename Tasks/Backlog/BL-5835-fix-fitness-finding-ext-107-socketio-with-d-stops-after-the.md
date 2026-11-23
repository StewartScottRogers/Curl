---
id: BL-5835
title: Fix fitness finding EXT-107: socketio with -d stops after the first event received, so the reply to the emitted event (or every later event) is lost
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5835 — Fix fitness finding EXT-107: socketio with -d stops after the first event received, so the reply to the emitted event (or every later event) is lost

## Goal

EXT-107 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: socketio with -d stops after the first event received, so the reply to the emitted event (or every later event) is lost. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** Medium (silent data loss against documented behaviour)
- **Confidence:** Confirmed (2 runs, two mock modes)
- **Protocol/area:** socketio, socketios
- **Repro:**
  1. A Socket.IO (EIO4) mock on 127.0.0.1:28913 that accepts the namespace CONNECT without sending an event, and on receiving the client's event sends `42["reply","ok"]`, then `42["reply2","ok2"]`, then `41` (disconnect).
  2. `curl -sS -m 5 -d '["ping",1]' socketio://127.0.0.1:28913/`
  3. Variant: a mock that pushes `42["hello","world"]` right after CONNECT and replies `42["reply","ok"]` to the client's event: curl writes only `["hello","world"]` and closes before the reply.
- **Expected:** `--extended-help` says socketio "emits the -d JSON array … and writes each event received as one JSON array per line until the server disconnects"; ADR-1035: "The transfer ends with exit 0 on DISCONNECT". Step 2 output: `["reply","ok"]` and `["reply2","ok2"]`, exit 0.
- **Actual (.NET):** one line `["reply","ok"]`, exit 0; the second event is silently dropped.
- **Likely location:** `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs` `WriteEventAsync`: `isEnded = emit is not null;` ends the session on the first event whenever `-d` was given.
- **Acceptance check:** step 2 prints both `["reply","ok"]` and `["reply2","ok2"]`, exit 0.

## Acceptance criteria

- [ ] step 2 prints both `["reply","ok"]` and `["reply2","ok2"]`, exit 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.