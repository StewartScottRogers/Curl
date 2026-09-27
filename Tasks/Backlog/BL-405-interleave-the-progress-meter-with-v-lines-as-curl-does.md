---
id: BL-405
title: Interleave the progress meter with -v lines as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-405 — Interleave the progress meter with -v lines as curl does

## Goal

Without `-s`, `-v` lines and the progress meter appear on standard error in the order curl 8.21.0 writes them.

## Context

- `Curl.Console` writes the meter after the transfer (BL-131), so under `-v` every `-v` line comes first and the meter after. curl writes the meter as the transfer goes.
- Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel): `Record-CurlExchange.ps1 -Port 18421 -Response 'HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\n\r\nhello\n' -CurlArgs @('-v','http://127.0.0.1:18421/f.txt','-o','o1')` wrote `Trying` and `Established connection`, then the two meter header lines and the zero status line with no line end, then `* using HTTP/1.x` straight after it, the request and response lines and `{ [6 bytes data]`, then three `\r100 ...` status lines and a line feed, then `* Connection #0 to host 127.0.0.1:18421 left intact`.

## Acceptance criteria

- [ ] Over a scripted handler, `-v` without `-s` writes standard error byte for byte as measured, meter and `-v` lines interleaved.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.

## Log

- 2026-09-27: Created.
