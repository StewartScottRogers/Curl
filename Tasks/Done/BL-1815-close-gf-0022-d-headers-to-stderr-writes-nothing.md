---
id: BL-1815
title: Close GF-0022: -D % (headers to stderr) writes nothing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1815 — Close GF-0022: -D % (headers to stderr) writes nothing

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0022 (-D % (headers to stderr) writes nothing), so a later gap analysis measures each of `behaviour:test1489` as `match`.

## Context

- Finding: GF-0022, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1489`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1489 (-D % -s) expected 'upstream test1489 passes', actual: <verify><stderr> differs at byte 0 (line 1): expected 'HTTP/1.1 200 OK', got the end. The reference curl exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1489

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary and Curl.Console's -D target opening, accept '%' as stderr (curl 8.21.0's -D %) and write the received headers there, even under -s. Keep --ai-help's dump-header entry right.

## Acceptance criteria

- [x] `behaviour:test1489`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured real curl 8.21.0 (Windows Schannel build) on 2026-10-08 with Record-CurlExchange.ps1
  against `HTTP/1.1 200 OK\r\nContent-Length: 3\r\nX: y\r\n\r\nabc`:
  - `-D % -s URL`: stderr is the head byte for byte (CR LF kept, no text-mode doubling), stdout `abc`, exit 0.
  - `-D % --stderr - -s URL`: the head still goes to stderr, not stdout; stdout is only `abc`.
  - `-D % --stderr <file> -s URL`: the head goes to the file; stderr is empty.
  So `%` means curl's C `stderr`, which `--stderr <file>` `freopen`s and `--stderr -` does not.
  The runner keeps that as `processStandardError` beside the `standardError` its own lines use.
- `--ai-help` needed no change: its `--dump-header` entry already carries curl's manual text
  ("Starting in curl 8.10.0, specify "%" ... writes the output to stderr").
- No ADR: matching measured curl, no design choice beyond it.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. -D % writes the received head to standard error even under -s, as curl 8.21.0 does (upstream test1489).
