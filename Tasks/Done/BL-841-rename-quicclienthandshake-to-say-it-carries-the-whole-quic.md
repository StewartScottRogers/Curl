---
id: BL-841
title: Rename QuicClientHandshake to say it carries the whole QUIC connection
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-726]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-841 — Rename QuicClientHandshake to say it carries the whole QUIC connection

## Goal

The I/O-free QUIC client state machine has a name that says it carries the connection (its handshake, its streams and flow control, its loss recovery), not only the handshake.

## Context

- Since BL-726, `QuicClientHandshake` in `Curl.Quic.UnitLibrary` also carries the connection's `Streams`, `TakeDatagramsToSend` and `CloseWithApplicationError`; ADR-0174 ("Consequences") notes its name no longer says all it does. Root `CLAUDE.md`: "Say what it does, do what it says".
- Rename it (for example to `QuicClientConnectionState`, avoiding a clash with `QuicConnection`), with `QuicClientHandshakeTests`, `QuicHandshakeTest` and the references in `QuicClientConnector`, `QuicConnection` and `Curl.Quic.UnitLibrary/CLAUDE.md`. Check first whether any project outside `Curl.Quic.*` references it (BL-728's `Curl.Networking` may by then); if one does, add it to `touches`.

## Acceptance criteria

- [x] No type named `QuicClientHandshake` remains in the solution; `Curl.Quic.UnitLibrary/CLAUDE.md` names the new type and says what it carries.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- 2026-09-29: `Curl.Networking.UnitLibrary/QuicDialer.cs` constructs and passes `QuicClientHandshake` (lines 152, 240, 272), so the rename must edit it; added `Curl.Networking.UnitLibrary` to `touches`. BL-850 (in Doing) touches that project, so the task went back to Backlog until BL-850 finishes.

- 2026-09-29: Renamed to `QuicClientConnectionState` (the task's suggestion; `QuicConnection` is already the async wrapper), with `QuicClientConnectionStateTests` and the test helper `QuicClientConnectionStateTest`. `QuicConnection`'s field is now `state`; its constructor parameter stays `handshake`, since it must be a completed handshake. ADRs keep the old name: they record the decision as it was made.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Networking.UnitLibrary (QuicDialer uses QuicClientHandshake), which BL-850 in Doing touches
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The I/O-free QUIC client state machine is QuicClientConnectionState, named for the whole connection it carries
