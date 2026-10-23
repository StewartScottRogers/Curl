---
id: BL-3244
title: Differential HTTP responder core: request reader, response writer, route registry, fall-through routes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3231]
touches: [Curl.Conformance.Differential.UnitLibrary/Http/Core, Curl.Conformance.Differential.UnitTests/Http/Core]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---

## Goal

The differential runner's UnitLibrary holds the stream-based HTTP/1.1 responder core that the route tasks (BL-3245 to BL-3250) build on: request reading, response writing, a self-registering route registry, per-run state and request log, and server.py's fall-through routes.

## Context

Reference: `Z:\repos\Curl.difftest\2026-10-22\server.py` (outside the repo, read only, never run Python). Parent: BL-3232, split for size on 2026-10-23. Runner ADR: ADR-0782 (BL-3231). The socket host is BL-3233's; this task is streams only.

Port, under `Curl.Conformance.Differential.UnitLibrary/Http/Core`:
- Request reading from a `Stream`: request line, headers (lower-cased names, last one wins, Latin-1), body by `Transfer-Encoding: chunked` (raw chunk framing kept, as server.py logs it) or `Content-Length`; `HTTP/1.1 100 Continue\r\n\r\n` written when an `Expect` header is present; absolute-form targets reduced to the path; path and query split at `?`; keep-alive loop until a route says close or the stream ends.
- The request log (raw head plus body of each request) and per-run state (counters), both cleared by a reset, as server.py's control port does (`/reset` clears both; any other path returns the log joined by `\0SEP\0` and clears it); a control responder answering those two over a stream.
- The response writer equivalent to `resp`: status line with server.py's reason table (`Status` for any code not in it), the fixed `Date: Thu, 01 Jan 2026 00:00:00 GMT`, headers in order, `Content-Length` added unless given or the status is 204, 304 or 101 or length is off, optional `Connection: close`; plus raw-bytes writing for routes that bypass it.
- A route contract (matches a method and path, writes the response, says whether the connection closes) and a registry that each route file adds itself to without a central switch and without reflection (Native AOT), and responder options carrying the port that some routes embed.
- server.py's own fall-through routes, one file each: `/status/<n>`, `/echo` and `/` (HEAD and GET), `CONNECT` (any path), and the default `default\n` answer for anything else. Route order matters only for the default and `CONNECT`, which run after every specific route.

## Acceptance criteria

- [x] Request reading, response writing, the registry, per-run state, the request log and the control responder are under Curl.Conformance.Differential.UnitLibrary/Http/Core, unit tested over in-memory streams.
- [x] The `/status/<n>`, `/echo`, `/`, `CONNECT` and default routes each have a responder file and a unit test pinning their response bytes.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Routes register from a [ModuleInitializer] in their own file into DifferentialRoutes (the pattern UpstreamPerlCommandScripts already uses); CA2255 is suppressed for the Curl.Conformance.Differential.Http namespace in Http/Core/RouteRegistrationSuppressions.cs. Order: DifferentialRouteRank (Specific, Connect, Default), stable by registration order within a rank.
- Sensible defaults taken: a route returns true to close the connection (server.py's return True); a request server.py would throw on (non-hex chunk size, non-numeric Content-Length, non-numeric /status/ segment, request line without a target) ends the connection as the Python exception did; resp's head is UTF-8 encoded like str.encode(); DifferentialResponderOptions carries Port and TimeProvider for BL-3250's delays.
- Fall-through routes live in Http/Core (StatusCodeRoute, EchoRoute, ConnectRoute, DefaultRoute), the folder this task touches.
- Checks: Curl.Conformance.Differential.UnitTests 65 passed with -warnaserror; dotnet format clean; Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 100% line, 100% branch, 0 failing members. Tests use only in-memory streams and no paths, so they are platform-neutral (Linux and macOS CI).
- Not changed: the library's CLAUDE.md Shape list (outside touches); a docs follow-up can add Http/Core to it.
- Filed by BL-3232's lane-1 run on 2026-10-23 when it split BL-3232 for size.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 7ca41420 5c45b8f1 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. Differential HTTP responder core: request reader, resp writer, route registry, state, control responder and fall-through routes, unit tested over in-memory streams
