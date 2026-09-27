---
id: BL-226
title: Format the -w time_* and speed_* values
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-225]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-226 — Format the -w time_* and speed_* values

## Goal

`%{time_*}` and `%{speed_*}` render from `TransferTimings` as curl does.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Times print with six decimals and speeds as measured on curl 8.21.0.
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-26, curl 8.21.0 (`/mingw64/bin/curl`, mingw, Schannel), with `-w 'ns=%{time_namelookup}|c=%{time_connect}|a=%{time_appconnect}|pre=%{time_pretransfer}|post=%{time_posttransfer}|st=%{time_starttransfer}|r=%{time_redirect}|t=%{time_total}|sd=%{speed_download}|su=%{speed_upload}'`:
  - `Record-CurlExchange.ps1 -Port 18226 -Response 'HTTP/1.1 200 OK
Content-Length: 20000

<20000 x>' -CurlArgs -s,-o,NUL,-d,<5000 y>,-w,<above>,http://127.0.0.1:18226/` printed `ns=0.000065|c=0.005721|a=0.000000|pre=0.006038|post=0.006038|st=0.057008|r=0.000000|t=0.057123|sd=350170|su=87542`.
  - The same without `-d` to `http://localhost:18227/` printed `...|t=0.251240|sd=79607|su=0`.
  - `file:///tmp/bl226.bin` (100000 bytes) printed all times at most microseconds and `sd=0|su=0`; `http://127.0.0.1:1/` (exit 7) printed `ns=0.000067|c=0.000000|a=0.000000|pre=2.027121|post=2.027122|st=2.027122|r=0.000000|t=2.027127|sd=0|su=0`.
  - Times: microseconds printed `%lu.%06lu`. Speeds: whole bytes per second, `size*1000000/us` truncated (350170 = 20000/0.057115, curl's last progress update, just before `time_total`).
- Decisions (ADR-0035, decided by Claude under Stewart's delegation): `TransferWriteOutVariables` takes a `TimeProvider` as its last constructor argument (nobody outside the tests constructs it yet; BL-235 wires it); times are truncated microseconds since `TransferTimings.Started`, at least 1 µs for an event that happened (`Curl_pgrsTime`); speeds divide by `time_total` with curl's `trspeed` overflow rules; no timings print `0.000000` and `0`.
- Added `Documentation` to `touches` for ADR-0035 and its index row; no task in Doing names it.
- Filed BL-287: curl takes a namelookup time for a literal address and pretransfer/starttransfer times after a refused connect; our handlers do not. That is where timestamps are taken, not formatting.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports 100% line, 100% branch, 69 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -w time_* print six-decimal microseconds and speed_* whole bytes per second as curl 8.21.0 does
