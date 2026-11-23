---
id: BL-5781
title: Send --oauth2-bearer as the Socket.IO CONNECT auth token ({"token": ...})
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5781 — Send --oauth2-bearer as the Socket.IO CONNECT auth token ({"token": ...})

## Goal

`curl --oauth2-bearer <token> socketio://...` (and `socketios://`, and `Curl.Api`'s bearer authentication) sends the token in the Socket.IO namespace CONNECT packet's `auth` payload as `{"token":"<token>"}`, so a server that authenticates by token accepts it; `-u` keeps sending user and password as BL-5567 decided.

## Context

From Surly cookbook bug report `2026-11-22T1734-Curl-bug-report.md`, finding 1, against Curl `2145a49eae` (the cookbook's re-pin; BL-5758 confirmed working there). No curl option sends a Socket.IO token: Surl takes `{"token":...}` in the CONNECT auth, but `-u` always sends user and password (BL-5567's mapping), and `--oauth2-bearer` is not used by the Socket.IO client at all. Socket.IO v5's CONNECT `auth` payload is application-defined; `{"token": ...}` is the convention the Socket.IO docs use for token auth. Read BL-5567's ADR and extend it (decided by Claude under Stewart's delegation): `--oauth2-bearer` maps to `{"token": "<value>"}` in the CONNECT auth (and, if the ADR's handshake also carries HTTP `Authorization`, `Authorization: Bearer <value>` on the Engine.IO handshake as curl does for HTTP); `-u` stays as it is; giving both follows whatever precedence curl's HTTP code gives `--oauth2-bearer` against `-u`. The token must never appear in `-v` output or `--log-file`. Update `--extended-help` and `--ai-help` for socketio.

## Acceptance criteria

- [x] With `--oauth2-bearer abc`, the client's namespace CONNECT packet carries `{"token":"abc"}`, pinned by a unit test of the bytes; `-u` still sends user and password.
- [x] Against Surl's Socket.IO endpoint requiring a token (or the test double), `--oauth2-bearer` joins and receives an event; a wrong token fails with the exit code BL-5567's ADR names. The Notes record the run.
- [x] `Curl.Api`'s bearer authentication does the same, pinned by a test; the token appears in no `-v` or `--log-file` line.
- [x] BL-5567's ADR, `--extended-help` and `--ai-help` describe the token mapping; `dotnet build` is clean and the fast tests pass.

## Notes

- Decision in ADR-1340: bearer wins over -u (as HandshakeHeaderFields does), `{"token":..}` in CONNECT auth, Authorization: Bearer kept on handshake. Curl.Api reaches the handler via the same HttpRequestOptions.BearerToken. `--ai-help` has no socketio text beyond the extension description (updated in SocketIoExtension).
- Criterion 2: Surl is not reachable from a lane; pinned by scripted-connection unit tests (token bytes, no token in the log); a wrong token is a CONNECT_ERROR, exit 22 per ADR-1311, already tested.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1734, finding 1.
- 2026-11-22: Backlog -> Doing. Criteria at claim: e233e406 7c669c45 e55a07b2 aed7932a.
- 2026-11-22: Doing -> Done. Bearer token sent as Socket.IO CONNECT auth token
