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
completed:
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

- [ ] An upload of known length whose first read fails leaves `connection.Written` empty and returns exit 26 with `client read function EOF fail, only 0/100000 of needed bytes read`, in a `HttpProtocolHandlerTests` test.
- [ ] A request that waits for `100 Continue` still sends its head before the wait (the existing tests stay green).
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes`, never a socket.

## Notes

## Log

- 2026-09-27: Created.
