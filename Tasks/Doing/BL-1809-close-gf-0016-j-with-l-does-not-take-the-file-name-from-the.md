---
id: BL-1809
title: Close GF-0016: -J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1809 — Close GF-0016: -J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0016 (-J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs), so a later gap analysis measures each of `behaviour:test1642`, `behaviour:test1643`, `behaviour:test3036` as `match`.

## Context

- Finding: GF-0016, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1642`, `behaviour:test1643`, `behaviour:test3036`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1642 (-J -L -O, no Content-Disposition): <verify><file> <LOGDIR>/16420002 differs at byte 0: expected '12345', got the end (no such file). test1643 (two redirects) has the same shape. test3036 (--no-clobber --output-dir ... -OJ --retry 1 --retry-all-errors): stderr differs from the reference curl, which exits 23. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1642,1643,3036

Suggestion, copied from the finding:

In Curl.Console's -O/-J output naming: with -J and -L and no Content-Disposition, name the file after the last URL followed (the final Location's last path segment), as curl 8.21.0 does. With --no-clobber and --retry, fail on an existing file with exit 23 and the reference's message, as upstream test3036 expects.

## Acceptance criteria

- [ ] `behaviour:test1642`: Curl answers what curl 8.21.0 answers, `upstream test1642 passes`, so the item measures `match`.
- [ ] `behaviour:test1643`: Curl answers what curl 8.21.0 answers, `upstream test1643 passes`, so the item measures `match`.
- [ ] `behaviour:test3036`: Curl answers what curl 8.21.0 answers, `reference curl exits 23; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
