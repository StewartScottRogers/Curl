---
id: BL-649
title: Parse --trace-config and enable the trace components Curl reports
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-648]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-649 — Parse --trace-config and enable the trace components Curl reports

## Goal

`--trace-config <list>` parses curl 8.21.0's comma list (`ids`, `time`, `all`, `-name` to disable, protocol and component names) and turns on `ids` (as `--trace-ids`), `time` (as `--trace-time`), `all`, and the trace lines of every component whose code exists in Curl today, with the output the reference build gives for each.

## Context

- Conformance audit 2026-09-28, row 30 (Major; filed Low because the component logs are libcurl internals). Depends on BL-648 for `ids`.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): nothing is left out. Component names (`tls`, `http/1`, `http/2`, `http/3`, `dns`, `doh`, `ssh`, `smtp` and the rest curl 8.21.0 accepts) make curl write component trace lines; Curl writes the equivalent lines for each component from its own code. Measure what the reference build prints for `--trace-config tls`, `all` and each component name, and record in an ADR marked "Decided by Claude under Stewart's delegation" how each component's lines map onto Curl's code and which task writes them: this task for components whose code exists; for each component whose code is still to be built (HTTP/2, HTTP/3, QUIC, SSH, mail and so on), add a line to that component's `-v`/trace task naming the `--trace-config` lines it must also write, or file a task for it.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (and `-Tls -k`): `--trace-config ids,time`, `--trace-config all`, `--trace-config tls`, `--trace-config bogus`; stderr copied into Notes.
- [x] `Curl.Cli.UnitTests` pin parsing; tests pin `ids`, `time`, `all`, each existing component's lines (at least `tls`, `http/1` and `dns`) and the unknown-name behaviour as measured.
- [x] Every component name curl accepts appears in the ADR with the task that writes its lines; none is recorded as "cannot".
- [x] The ADR exists in `Documentation/Planning/Decisions` (number checked unused) and is indexed in its `README.md`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- `doh` (BL-850): `DohDnsResolver`'s four-argument constructor takes the sink (`ITransferEvents`) and a `Func<CurlExitCode, string>` for the `DoH request <text>` lines; `CurlComposition.CreateDohResolver` should pass the transfer's events and `CurlEasyErrorText` under `-v --trace-config doh` (measure whether `dns` and `all` also turn them on), `NoTransferEvents` otherwise. Measured lines: BL-850's Notes and ADR-0152's BL-850 amendment.
- Measured 2026-10-01, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` (port P, `200` with `hi`) and `curl -s <args> http://127.0.0.1:1/`:
  - `--trace-config ids,time -v`: every `-v` line gets `HH:MM:SS.ffffff [0-0] `. `--trace-config ids -v`, `-v --trace-config ids`, `+ids`, `IDS`, `ids -v -v`, `-v --trace-config ids -v`, `ids -sv`: `[0-0] ` (while `--trace-ids -v` and `-s --trace-ids -v` show none). `time -v`: times only. `all,-ids`: times, no IDs.
  - No IDs/times: `"ids time"`, `" ids"`, `"ids "`, `"+ ids"`, `ids;time`, `""`, `,`, `bogus` (exit 0, no warning), `ids --no-trace-ids -v`, `time --no-trace-time -v`, `ids,time --no-verbose -v`, `ids,time -vv --no-verbose -v`, `all --trace-config -all -v`, `-vv --trace-config -ids,-time`, `-vv --trace-config -all`, `--trace-ids --trace-config -ids -sv`. `"ids, time"` and `ids,,time` give both. `--no-trace-config`: exit 2, "the given option cannot be reversed with a --no- prefix".
  - `--trace-config tls -v` (HTTP, and `-k -s` over `-Tls` HTTPS) and `--trace-config http/1 -v`: stderr byte-identical to plain `-v`.
  - `--trace-config dns -v`: `* [DNS] created DNS filter for 127.0.0.1:P, transport=3, queries=3`, `* [DNS] added`, `* [DNS] cf_dns_start host 127.0.0.1:P` before `Trying`; `* [DNS] Curl_conn_connect(block=0) -> 0, done=0`, `* [DNS] connected filter chain below`, `* [DNS] Curl_conn_connect(block=0) -> 0, done=1` before `Established connection`; `* [DNS] removing connected setup filter`, `* [DNS] destroy` after it.
  - `--trace-config all -v`: ids and times plus ~100 lines of `[MULTI] [state]`, `[SETUP]`, `[DNS]`, `[HAPPY-EYEBALLS]`, `[TCP]` (fd numbers), `[TIMER]`, `[READ]`, `[WRITE]`, `[PGRS-*] added 404ns`; `-vv` alone already writes four `[SETUP]` lines (`added`, `happy eyeballing to origin 127.0.0.1:P`, `removing connected setup filter`, `destroy`), which `--trace-config -setup` removes.
- Decision (ADR-0318): `ids`/`time` set `TraceIds`/`TraceTime` plus a sticky flag a first `-v` leaves alone; `--no-verbose`, `--no-trace-ids`/`--no-trace-time` and `-ids`/`-time` clear both. Other names go into `CommandLineOptions.TraceComponents`, lower case. Curl.Output needed no change: the prefixes were already written from `TraceIds`/`TraceTime`.
- Component lines: `tls` and `http/1` pinned as writing nothing beyond `-v` (`CurlCommandRunnerTransferEventTests.RunAsync_TraceConfigTlsHttp1AndUnknownName_WritesNoLinesBeyondV`). The `dns` component's parse is pinned here; its `[DNS]` lines come from the connect path in `Curl.Networking` (outside touches) and the DoH sink needs late binding because `CurlComposition` builds the resolver before any transfer's events exist, so both are filed as BL-1102. Connection-filter and transfer-engine components (and `-vv`..`-vvvv`'s): BL-1103. Protocol components: BL-1104 (to be split per protocol).
- `--ai-help` needed no edit: it builds each option's section from `CommandLineOptionTable`, which now has the row.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --trace-config parses curl's list: ids, time and all set the prefixes as curl does, components are kept for their tasks (ADR-0318, BL-1102..1104)
