---
id: BL-333
title: Hold the HTTP request head until the first upload read succeeds
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-184]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-333 — Hold the HTTP request head until the first upload read succeeds

## Goal

When the first read of an HTTP upload fails, the handler sends no request bytes at all, as curl 8.21.0 does.

## Context

- Measured in BL-184 (ADR-0055): `curl -T big.bin` with every byte of the 100000-byte file locked sent nothing to the server and exited 26 `client read function EOF fail, only 0/100000 of needed bytes read`; curl holds the head in its upload buffer with the first body read. The handler writes and flushes the head first, so a server sees the head.
- Only when the request does not wait for `100 Continue`: then curl sends the head alone.
- Start in `HttpProtocolHandler` (the head write and `SendBodyAsync`) and `HttpRequestBodyWriter`.
- Filed from BL-184.

## Acceptance criteria

- [x] An upload of known length whose first read fails leaves `connection.Written` empty and returns exit 26 with `client read function EOF fail, only 0/100000 of needed bytes read`, in a `HttpProtocolHandlerTests` test.
- [x] A request that waits for `100 Continue` still sends its head before the wait (the existing tests stay green).
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes`, never a socket.

## Notes

- The head now travels in `HttpRequestBodyWriter.HeldHead` and is written and flushed by `WriteHeldHeadAsync` just before the first body bytes (or at the end of `WriteAsync` for an empty body). `SendBodyAsync` sends it alone, up front, when there is no body or the request waits for `100 Continue`, so those paths are unchanged. No ADR: this only brings the handler in line with behaviour already measured and recorded in BL-184 (ADR-0055).
- Verified: `dotnet build` clean, all fast tests green (912 HTTP), `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A failed first upload read now sends no request bytes; build clean, fast tests green, 100% coverage.
