---
id: BL-1183
title: Write the [WRITE] client writer trace lines for --trace-config write and -vvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1159]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1183 — Write the [WRITE] client writer trace lines for --trace-config write and -vvv

## Goal

Curl writes curl 8.21.0's `[WRITE]` client writer lines under `--trace-config write`, `all`, `-vvv` and `-vvvv`.

## Context

- Split from BL-1159 (ADR-0357 amendment); `-vvv` already puts `write` in `CommandLineOptions.TraceComponents`. BL-1103 Notes hold the measured lines: after each `< ` header line `[WRITE] [OUT] wrote 17 header bytes -> 17`, `[WRITE] [PAUSE] writing 17/17 bytes of type c -> 0`, `[WRITE] download_write header(type=c, blen=17) -> 0`, `[WRITE] client_write(type=c, len=17) -> 0` (later headers type 4, preceded by `[WRITE] header_collect pushed(type=1, len=19) -> 0`); after the body `[OUT] wrote 2 body bytes -> 2`, type 1, `xfer_write_resp(len=40, eos=0) -> 0`, then `[WRITE] [OUT] done`. Re-measure with `Record-CurlExchange.ps1` before pinning. Written where the HTTP handler hands header and body bytes on.

## Acceptance criteria

- [ ] Tests pin the `[WRITE]` lines of a plain HTTP 200 with a two-byte body, and that none appears without `write` or `all`; an ADR-0357 amendment records how `xfer_write_resp`'s length is produced.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
