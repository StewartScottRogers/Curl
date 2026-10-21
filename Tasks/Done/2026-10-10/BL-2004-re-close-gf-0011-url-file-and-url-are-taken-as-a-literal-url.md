---
id: BL-2004
title: Re-close GF-0011: --url @file and --url @- are taken as a literal URL instead of a list of URLs to read
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2004 — Re-close GF-0011: --url @file and --url @- are taken as a literal URL instead of a list of URLs to read

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0011 (--url @file and --url @- are taken as a literal URL instead of a list of URLs to read), so a later gap analysis measures each of `behaviour:test488`, `behaviour:test489`, `behaviour:test2012` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0011 ([BL-1804]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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

- No code change was needed: BL-1804's fix (commit 14cda1b4a, plus BL-1832's noglob) is in this tree and reached `master` in merge 696857941 at 2026-10-10 05:32, before the 06:57 gap run that still measured the gap.
- Measured on this tree's `Curl.Console` build (`curl.exe`) with `Record-CurlExchange.ps1`, running each upstream case's command line (taken from curl 8.21.0's `tests/data` on GitHub, since lanes may not read the local upstream copy, which sits under a `gap` path):
  - test488 `--output-dir <dir> --url @-`, stdin two URLs: requests `GET /a`, `GET /b`, exit 0, stdout empty.
  - test489 `--output-dir <dir> --url @<dir>/urls`: same two GETs, exit 0 (also checked by hand from PowerShell with a file of two URLs).
  - test2012 `-T '<dir>/upload{1,2}' --url @<dir>/urls --silent --output=<dir>/first-out --output=<dir>/second-out`: `PUT /2012` with upload1, `PUT /2012` with upload2, `GET /20120002`, exit 0, stdout empty - exactly the upstream `<protocol>`.
- The finding's evidence (`GET / HTTP/1.1` for 488, `GET /repos/Curl.gap/.../urls` for 489 and 2012) is what Curl gives when the `@` value is taken as a literal URL, which this tree's `CommandLineOptionTable.AddUrl` no longer does. So the gap run did not run this code as a standalone `curl.exe` does: either it measured an older build, or its harness feeds the arguments or stdin to Curl differently (compare BL-1997, which found the gap harness runs Curl in-process). That is in `Gap/`, which a lane may not read; an interactive session should look at `Gap/Tools/Measure-UpstreamCases.cs` before GF-0011 is re-measured. `--ai-help`: no option changed.


## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Curl already answers upstream tests 488, 489 and 2012 as curl 8.21.0 does on this tree; the gap run did not measure this code path
