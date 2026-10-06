---
id: BL-277
title: Write the progress meter for a followed redirect and before exit 47 in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-234]
touches: [Curl.Console, Curl.Console.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-277 — Write the progress meter for a followed redirect and before exit 47 in Curl.Console

## Goal

Under `-L`, standard error carries curl 8.21.0's progress-meter lines for every hop, and a transfer that ends with exit 47 prints the meter's opening before its `curl: (47)` line, as measured.

## Context

- Found in BL-234. `CurlCommandRunner.WriteProgressMeterAsync` writes the meter's opening once, after a success or an exit 22 only.
- Measured on curl 8.21.0 (mingw, Schannel), 2026-09-26: `curl -L -i http://127.0.0.1:18246/a` with `/a` a `302` to `/b` and `/b` a `200` wrote the two meter header lines, then one status line per hop (each a CR-separated run of updates ending in CRLF). `curl -L --max-redirs 0 -i ...` wrote the two header lines and one status line before `curl: (47) Maximum (0) redirects followed`.
- Live counters are BL-130 to BL-132; this task may pin only the lines those tasks leave all-zero, and should depend on them if the counters are needed.

## Acceptance criteria

- [x] A `CurlCommandRunner` test with `writesProgressMeter: true` pins the measured standard error for `-L -i` over a 302 then 200.
- [x] A test pins the meter opening before `curl: (47) Maximum (0) redirects followed` for `-L --max-redirs 0`.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass and `Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

- Measured 2026-09-27 with `Record-CurlExchange.ps1` (curl 8.21.0 mingw, Schannel), `/a` a `302` with an empty body to `/b`, `/b` a `200` with `hello`: `-L -i` wrote the header lines, `/a`'s line (zero line three times, CR-separated) + CRLF, then `/b`'s line (zero line, then the done line three times) + CRLF. Three runs of four gave exactly that; one gave `/a` an extra all-zero line, which is curl's draw timer on a slow hop. A two-redirect chain gave one line per hop. `-L --max-redirs 0 -i` gave the header lines, the redirect hop's line (zero three times) + CRLF, then `curl: (47) Maximum (0) redirects followed`.
- `Record-CurlExchange.ps1` extended: `-Response` now takes one response per connection (the last repeats), which a 302-then-200 chain needs. Added `Record-CurlExchange.ps1` to `touches` for it; no task in `Doing` names it.
- Decision (ADR-0086, decided by Claude under Stewart's delegation): `TransferProgressRecorder` reads a repeated `ReportTransferStarted()` as the next hop starting - it finishes the hop before with two done draws, ends it with a newline and starts the next from zero. Exit 47 gives the refused hop the same two draws (`CurlCommandRunner.FinishTransferProgress`). No change to `Curl.Core` or the Abstractions contract. Added `Documentation/Planning/Decisions` to `touches` for the ADR and its index line; no task in `Doing` names it.
- Tests: `CurlCommandRunnerRedirectProgressMeterTests` (302 then 200 with `-L -i`; `--max-redirs 0` exit 47; a three-hop chain), each read on a `ClockAdvancingConnector` taking 40 ms of the manual clock; two new `TransferProgressRecorderTests`.
- Verified: `dotnet build -warnaserror` clean; fast tests green (Curl.Console.UnitTests 818 passed); `dotnet format --verify-no-changes` clean. `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Without `-IncludeIntegration` the only failing member is `DiskWriteOutFileOpener.TryOpen`, which only Integration tests cover by design (BL-280); this task did not change it.
- Not measured: a hop that fails before connecting leaves the previous hop's line open (ADR-0086, Consequences).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Under -L the progress meter draws one status line per hop, and exit 47 prints the meter opening first, as curl 8.21.0 does
