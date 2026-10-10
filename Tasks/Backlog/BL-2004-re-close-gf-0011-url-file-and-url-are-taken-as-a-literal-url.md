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
completed:
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

- [ ] `behaviour:test488`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test489`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test2012`: Curl answers what curl 8.21.0 answers, `upstream test2012 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
