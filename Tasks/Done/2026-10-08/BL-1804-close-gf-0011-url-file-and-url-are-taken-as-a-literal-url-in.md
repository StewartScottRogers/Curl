---
id: BL-1804
title: Close GF-0011: --url @file and --url @- are taken as a literal URL instead of a list of URLs to read
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1804 — Close GF-0011: --url @file and --url @- are taken as a literal URL instead of a list of URLs to read

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0011 (--url @file and --url @- are taken as a literal URL instead of a list of URLs to read), so a later gap analysis measures each of `behaviour:test488`, `behaviour:test489`, `behaviour:test2012` as `match`.

## Context

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

- Behaviour taken from curl's own source (`parse_url`, `add_url`, `my_get_line` in src/tool_getparam.c and src/tool_parsecfg.c) and upstream tests 488, 489 and 2012: each line of the file (or stdin for `@-`) is one URL, a line that is blank or whose first non-blank character is `#` is skipped, and each URL is saved under its remote name (`useremote`) unless a `-o` is paired with it, `-o` and `-T` pairing in order as for any URL.
- A file that cannot be opened is refused with exit 26 and only `curl: option --url: error encountered when reading a file`: `parse_url` prints no `Failed to open` line, so it gets its own `CommandLineRefusal.UrlFileUnreadable`.
- Default taken: a trailing CR is dropped from each line (curl opens the file in text mode, which drops it on Windows), and the bytes are read as UTF-8, as other `@file` values are.
- `--ai-help`: no change needed; its `--url` section is curl 8.21.0's own manual text, which this change does not alter.
- Left for BL-1832: curl also sets `noglob` on file URLs; Curl still globs them per option group, which needs `Curl.Console` (outside this task's touches). The three upstream tests use no glob characters in their URLs.
- The gap closes only when a later gap analysis re-measures the three items; the lane cannot read `Gap/` to rerun it.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. --url @file and --url @- add one URL per line, each saved under its remote name; per-URL noglob left to BL-1832.
