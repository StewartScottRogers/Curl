---
id: BL-4610
title: STOMP SEND takes the content-type a -H header gives
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompClientFlow.cs, Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs, Curl.Extended.Protocol.Stomp.UnitTests/Client]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4610 — STOMP SEND takes the content-type a -H header gives

## Goal

A `-H "content-type: <type>"` given with `stomp://` and `-d` replaces the default `application/octet-stream` on the SEND frame.

## Context

ADR-1209 (BL-4609) gives every SEND `content-type:application/octet-stream` and leaves the user's `-H` unread. `StompProtocolHandler` passes `context.PostData` to `StompClientFlow.RunAsync`; the `-H` headers are in `HttpRequestOptions.Headers`. Amend ADR-1209 (or add an ADR) for the override.

## Acceptance criteria

- [x] A unit test in `Curl.Extended.Protocol.Stomp.UnitTests` shows `-H "content-type: text/plain"` puts `content-type:text/plain` on SEND, and only once.
- [x] Without `-H` the SEND still carries `application/octet-stream`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

ADR-1217 amends ADR-1209. Last matching -H wins; empty value ignored. Quality run: 0 failing members. Dotnet build of the test project (-warnaserror) clean, 61 tests green.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 9eb1466c 42b9926e e3a4fd8b.
- 2026-11-02: Doing -> Done. SEND takes -H content-type; ADR-1217
