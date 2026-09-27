---
id: BL-302
title: Report whether a proxy was used and supply -w proxy_used
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-302 — Report whether a proxy was used and supply -w proxy_used

## Goal

`%{proxy_used}` prints `1` when the transfer went through a proxy and `0` otherwise, as curl 8.21.0 does.

## Context

- Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel); commands and bytes are in BL-284's Notes. ADR-0043 records why BL-284 left these unknown.
- Measured: `0` for file:// and a direct http:// transfer. Measure a transfer through a loopback HTTP proxy (`-x`) and a CONNECT tunnel before pinning `1`.
- The source is the handler, which knows whether it connected to a proxy: record in an ADR a `TransferReport` member (ADR-0015 says a later ADR adds it), set it in the HTTP handler, and print it in `TransferWriteOutVariables`.

## Acceptance criteria

- [ ] An ADR names the `TransferReport` member and when it is set.
- [ ] `proxy_used` renders `0` for file:// and direct http:// and `1` through a proxy, as measured, pinned in `TransferWriteOutVariablesTests` and the HTTP handler's tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every project touched.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Filed by BL-284.
- 2026-09-27: Backlog -> Doing.
