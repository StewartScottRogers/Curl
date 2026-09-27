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
completed:
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

- [ ] Tests show each of the three commands changes where the request ends exactly as sws does,
      and none of them is listed in `UnsupportedServerCommands` any more.
- [ ] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10,
      per `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
