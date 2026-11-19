---
id: BL-5567
title: Send -u credentials from the socketio client so it can join a server that requires them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs, Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs, Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoPacket.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5567 — Send -u credentials from the socketio client so it can join a server that requires them

## Goal

The `socketio://` client sends the `-u` user and password to the server, so it can join a Socket.IO server that requires credentials, not only one started with `--allow-anonymous`.

## Context

From bug report `2026-11-19T0930-Curl-bug-report.md`, finding 4 (Low). Build tested: commit `82913be53d`, Windows 11; server Surl at `71807f05f`. The *Surly* recipe `socketio-receive-an-event` has a case showing it, with the exact command in its `review.md`.

The client ignores `-u`, so it can join only a server that allows anonymous clients.

**Spec:** Socket.IO protocol v5 carries credentials in the `auth` payload of the namespace CONNECT packet (`0{...}`); the Engine.IO handshake can also carry HTTP `Authorization`. Find out which form Surl's Socket.IO server checks for `-u` (and what the reference Socket.IO server does), and send `-u` that way; send the HTTP `Authorization` header on the handshake request too if the spec and Surl expect it. Record the mapping from `-u` to the wire in an ADR (decided by Claude under Stewart's delegation), and update `--extended-help` and `--ai-help` where they describe `socketio`.

**Where to start:** `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoSession.cs`, `SocketIoUpgrade.cs` and `SocketIoPacket.cs`. No credential may appear in `-v` output or `--log-file` (the messaging schemes' rule).

## Acceptance criteria

- [ ] With `-u user:pass`, the client's namespace CONNECT packet (or handshake, per the ADR) carries the credentials, pinned by a unit test of the bytes sent.
- [ ] Against a Surl Socket.IO server that requires credentials (no `--allow-anonymous`), `curl -u user:pass socketio://...` receives an event and exits 0; with a wrong password it fails with the exit code the ADR names.
- [ ] The password does not appear in `-v` output or `--log-file`, pinned by a test.
- [ ] Focused unit tests in the Socket.IO library's `.UnitTests` pin each case above.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created from bug report 2026-11-19T0930-Curl-bug-report.md, finding 4.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 5593b376 1e2cc757 789553a4 9dfb5f68 3d836d5e.
