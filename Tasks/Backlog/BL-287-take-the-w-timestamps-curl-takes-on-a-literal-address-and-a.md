---
id: BL-287
title: Take the -w timestamps curl takes on a literal address and a refused connect
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-287 — Take the -w timestamps curl takes on a literal address and a refused connect

## Goal

`%{time_namelookup}` for a literal address and `%{time_pretransfer}`, `%{time_posttransfer}` and `%{time_starttransfer}` after a refused connect print non-zero, as curl 8.21.0 prints them.

## Context

- Found by BL-226, which formats the `-w` times from `TransferTimings` (ADR-0035, "Consequences"). The formatting is right; the timestamps are not taken where curl takes them.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-26: `curl -s -o NUL -w "ns=%{time_namelookup}|c=%{time_connect}|pre=%{time_pretransfer}|post=%{time_posttransfer}|st=%{time_starttransfer}|t=%{time_total}" http://127.0.0.1:1/` printed `ns=0.000067|c=0.000000|pre=2.027121|post=2.027122|st=2.027122|t=2.027127`, exit 7. A literal `127.0.0.1` to a live loopback server printed `ns=0.000065`.
- Today `ConnectTimings.NameResolved` is `null` for a literal address (ADR-0030) and a refused connect reports no timings, so these print `0.000000`. Decide, with an ADR, whether the connector records a resolution for a literal and whether a failed transfer reports its timings.

## Acceptance criteria

- [ ] A test pins non-zero `time_namelookup` for a literal address and the measured pretransfer/posttransfer/starttransfer behaviour after a refused connect, or an ADR records why not.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
