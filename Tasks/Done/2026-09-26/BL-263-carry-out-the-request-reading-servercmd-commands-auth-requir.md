---
id: BL-263
title: Carry out the request-reading servercmd commands auth_required, no-expect and skip in the sws emulation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-146]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-263 — Carry out the request-reading servercmd commands auth_required, no-expect and skip in the sws emulation

## Goal

`SwsHttpServerConnector` carries out the `<servercmd>` commands that change how sws reads a
request - `auth_required`, `no-expect` and `skip: N` - and stops listing them in
`UnsupportedServerCommands`.

## Context

- BL-146 built the emulation and reports these commands as unsupported (`SwsServerCommands`).
- Upstream: `tests/server/sws.c` at `curl-8_21_0`, `sws_parse_servercmd` and
  `sws_ProcessRequest`: `auth_required` ends the request at its headers when it carries no
  `Authorization:` header (test 154); `no-expect` ignores the body of a request carrying
  `Expect: 100-continue`; `skip: N` reads N bytes less than `Content-Length` says.

## Acceptance criteria

- [x] Tests show each of the three commands changes where the request ends exactly as sws does,
      and none of them is listed in `UnsupportedServerCommands` any more.
- [x] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10,
      per `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Delivered in-session rather than through the full `/feature` stages: the change is three
  small rules inside one library already built by BL-146, pinned against upstream `sws.c` at
  `curl-8_21_0` (`sws_parse_servercmd`, `sws_ProcessRequest`), fetched and read line by line.
- `SwsServerCommands` now reads `RequiresAuthorization`, `IgnoresExpectedBody` and
  `SkippedBodyBytes` (last `skip:` wins, as sws overwrites `req->skip`); `SwsHttpRequestFraming`
  applies them. Only `idle`, `stream`, `connection-monitor`, `upgrade`, `delay` and
  `writedelay` are still reported unsupported.
- `auth_required`: like sws, it looks for `Authorization:` anywhere in the bytes received for
  the request (sws's `strstr` over `reqbuf`), and a chunked request is never cut short because
  sws returns from the chunked check before the auth check.
- `no-expect`: sws zeroes the length when it meets `Expect: 100-continue` and ignores any
  `Content-Length` after it, so the header zeroes the body whichever side it is on.
- `skip: N`: `Content-Length - N`, taken from the first `Content-Length` that leaves a non-zero
  length. A result below zero never completes, as sws's `size_t` wraps to a huge length. A
  number too large for an `int` reads as 0 (C leaves sscanf's overflow undefined; no upstream
  case uses one).
- Default taken: bytes the client sent past an early end start the next request. sws discards
  what it has already read into its buffer and reads the rest off the socket as the next request,
  which depends on timing; keeping them is the deterministic reading the emulation already uses
  for pipelined requests, and it matches what upstream cases 88, 1130 and 357 send (no body
  follows the early end).
- Quality gate: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 100%
  line, 100% branch, 0 failing members, worst CRAP 10. Conformance tests 228, all passing.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. sws emulation carries out auth_required, no-expect and skip: N; only idle, stream, connection-monitor, upgrade, delay, writedelay remain unsupported
