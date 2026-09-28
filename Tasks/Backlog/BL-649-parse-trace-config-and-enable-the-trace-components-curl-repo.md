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
completed:
---
# BL-649 — Parse --trace-config and enable the trace components Curl reports

## Goal

`--trace-config <list>` parses curl 8.21.0's comma list (`ids`, `time`, `all`, `-name` to disable, protocol and component names) and turns on `ids` (as `--trace-ids`), `time` (as `--trace-time`), `all`, and the trace lines of every component whose code exists in Curl today, with the output the reference build gives for each.

## Context

- Conformance audit 2026-09-28, row 30 (Major; filed Low because the component logs are libcurl internals). Depends on BL-648 for `ids`.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): nothing is left out. Component names (`tls`, `http/1`, `http/2`, `http/3`, `dns`, `doh`, `ssh`, `smtp` and the rest curl 8.21.0 accepts) make curl write component trace lines; Curl writes the equivalent lines for each component from its own code. Measure what the reference build prints for `--trace-config tls`, `all` and each component name, and record in an ADR marked "Decided by Claude under Stewart's delegation" how each component's lines map onto Curl's code and which task writes them: this task for components whose code exists; for each component whose code is still to be built (HTTP/2, HTTP/3, QUIC, SSH, mail and so on), add a line to that component's `-v`/trace task naming the `--trace-config` lines it must also write, or file a task for it.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (and `-Tls -k`): `--trace-config ids,time`, `--trace-config all`, `--trace-config tls`, `--trace-config bogus`; stderr copied into Notes.
- [ ] `Curl.Cli.UnitTests` pin parsing; tests pin `ids`, `time`, `all`, each existing component's lines (at least `tls`, `http/1` and `dns`) and the unknown-name behaviour as measured.
- [ ] Every component name curl accepts appears in the ADR with the task that writes its lines; none is recorded as "cannot".
- [ ] The ADR exists in `Documentation/Planning/Decisions` (number checked unused) and is indexed in its `README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
