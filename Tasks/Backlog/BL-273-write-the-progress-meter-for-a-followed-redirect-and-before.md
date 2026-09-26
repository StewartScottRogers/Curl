---
id: BL-273
title: Write the progress meter for a followed redirect and before exit 47 in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-234]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-273 — Write the progress meter for a followed redirect and before exit 47 in Curl.Console

## Goal

Under `-L`, standard error carries curl 8.21.0's progress-meter lines for every hop, and a transfer that ends with exit 47 prints the meter's opening before its `curl: (47)` line, as measured.

## Context

- Found in BL-234. `CurlCommandRunner.WriteProgressMeterAsync` writes the meter's opening once, after a success or an exit 22 only.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-26: `curl -L -i http://127.0.0.1:18246/a` with `/a` a `302` to `/b` and `/b` a `200` wrote the two meter header lines, then one status line per hop (each a CR-separated run of updates ending in CRLF). `curl -L --max-redirs 0 -i ...` wrote the two header lines and one status line before `curl: (47) Maximum (0) redirects followed`.
- Live counters are BL-130 to BL-132; this task may pin only the lines those tasks leave all-zero, and should depend on them if the counters are needed.

## Acceptance criteria

- [ ] A `CurlCommandRunner` test with `writesProgressMeter: true` pins the measured standard error for `-L -i` over a 302 then 200.
- [ ] A test pins the meter opening before `curl: (47) Maximum (0) redirects followed` for `-L --max-redirs 0`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass and `Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

## Log

- 2026-09-26: Created.
