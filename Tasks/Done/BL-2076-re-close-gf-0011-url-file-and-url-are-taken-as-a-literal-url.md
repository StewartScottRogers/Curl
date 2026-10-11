---
id: BL-2076
title: Re-close GF-0011: --url @file and --url @- are taken as a literal URL instead of a list of URLs to read
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2076 — Re-close GF-0011: --url @file and --url @- are taken as a literal URL instead of a list of URLs to read

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0011 (--url @file and --url @- are taken as a literal URL instead of a list of URLs to read), so a later gap analysis measures each of `behaviour:test488`, `behaviour:test489`, `behaviour:test2012` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0011 ([BL-1804, BL-2004]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0011, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test488`, `behaviour:test489`, `behaviour:test2012`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test488 (--url @-) actual: <verify><protocol> differs at byte 5 (line 1): expected 'GET /a HTTP/1.1', got 'GET / HTTP/1.1'. test489 (--url @%LOGDIR/urls): expected 'GET /a HTTP/1.1', got 'GET /repos/Curl.gap/.../urls HTTP/1.1'. test2012: expected 'PUT /2012 HTTP/1.1', got 'PUT /repos/Curl.gap/.../urls HTTP/1.1'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 488,489,2012

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's --url option (CommandLineOptionTable / its applier), support curl 8.21.0's '@file' and '@-' forms: read the file or stdin, add one URL per non-blank line, and pair them with -o/--output and -T in order. Keep --ai-help's url entry right.

## Acceptance criteria

- [x] `behaviour:test488`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test489`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test2012`: Curl answers what curl 8.21.0 answers, `upstream test2012 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-11 (lane 3), found before any edit: the Cli side is already right. `CommandLineOptionTable.AddUrl` reads `@file`/`@-` (BL-1804), and the ratchet `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` already lists 489 and 2012 as passing in process. 488 (`--url @-`) is not listed. Cause: `Curl.Console/CurlComposition.cs`'s `CreateRunner` overloads (lines ~1100-1270) pass the injected `standardInput` only to telnet and never pass a `configFileReader`. So `CurlCommandRunner.DataFileReader` falls back to `DiskDataFileReader.ForProcess`, which reads `Console.OpenStandardInput()`, not the caller's stream. Every in-process `@-` (`--url @-`, `-d @-`, `-K -` and the rest) reads the host process's console. Fix: have `CreateRunner` build a `DiskDataFileReader` whose `openStandardInput` returns the injected `standardInput` and pass it as `configFileReader`. Add an `InProcessCurlTests` case for `--url @-`, then add 488 to the ratchet list. The ratchet file is in Curl.Conformance.UnitTests, which also needs adding to `touches` when the task is next claimed, if no live task holds it.
- `Curl.Console` and `Curl.Console.UnitTests` were added to `touches` for that fix. BL-2073 (in Doing) touches them, so the task goes back to Backlog until BL-2073 is done.
- 489/2012 evidence (the file path taken as a literal URL) is not reproduced in this tree, either in process (ratchet) or by `curl.exe` (BL-2004). If the next gap run still shows it, an interactive session must check which build `Gap/Tools/Measure-UpstreamCases.cs` runs. A lane may not read `Gap/`.

- 2026-10-11 (lane 1): fixed. `CurlCommandRunner.DataFileReader` now falls back to `DiskDataFileReader.ForProcess.WithStandardInput(() => standardInput)` (new in Curl.Cli), so every in-process `@-` reads the runner's standard input, whichever `CreateRunner` overload built it; fixing it in the runner rather than each `CreateRunner` overload covers all three at once. 488 added to `PassingUpstreamCases.txt` and passes. `Curl.Conformance.UnitTests` added to `touches` for that list; no other task was in Doing on origin/work/dark-factory. No option changed, so `--ai-help` needs nothing. 489 and 2012 already pass in process.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. Needs Curl.Console (CurlComposition must hand the injected stdin to the data file reader), which BL-2073 in Doing touches
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. In-process @- reads the runner's standard input; upstream test 488 passes in process, 489 and 2012 already did
