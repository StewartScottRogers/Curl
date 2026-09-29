---
id: BL-834
title: Rename QuicClientHandshake to say it carries the whole QUIC connection
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-726]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-834 — Rename QuicClientHandshake to say it carries the whole QUIC connection

## Goal

The I/O-free QUIC client state machine has a name that says it carries the connection (its handshake, its streams and flow control, its loss recovery), not only the handshake.

## Context

- Since BL-726, `QuicClientHandshake` in `Curl.Quic.UnitLibrary` also carries the connection's `Streams`, `TakeDatagramsToSend` and `CloseWithApplicationError`; ADR-0172 ("Consequences") notes its name no longer says all it does. Root `CLAUDE.md`: "Say what it does, do what it says".
- Rename it (for example to `QuicClientConnectionState`, avoiding a clash with `QuicConnection`), with `QuicClientHandshakeTests`, `QuicHandshakeTest` and the references in `QuicClientConnector`, `QuicConnection` and `Curl.Quic.UnitLibrary/CLAUDE.md`. Check first whether any project outside `Curl.Quic.*` references it (BL-728's `Curl.Networking` may by then); if one does, add it to `touches`.

## Acceptance criteria

- [ ] No type named `QuicClientHandshake` remains in the solution; `Curl.Quic.UnitLibrary/CLAUDE.md` names the new type and says what it carries.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-28: Created.
