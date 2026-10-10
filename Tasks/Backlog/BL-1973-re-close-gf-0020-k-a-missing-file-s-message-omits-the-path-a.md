---
id: BL-1973
title: Re-close GF-0020: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1973 — Re-close GF-0020: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0020 (-K: a missing file's message omits the path, and a Unicode quote in a config file is read differently), so a later gap analysis measures each of `behaviour:test411`, `behaviour:test470` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0020 ([BL-1813]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0020, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test411`, `behaviour:test470`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both items expect 'upstream test<N> passes'. test411 (-K <LOGDIR>/missing): <verify><stderr> differs at byte 30 (line 1): expected "curl: cannot read config from '<LOGDIR>/missing'", got 'curl: cannot read config from '. test470 (config file with a Unicode quote character): request differs at byte 77 from the reference curl, which exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 411,470

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's config-file reader: put the quoted path in 'cannot read config from '<path>''. Read a Unicode quote character (U+201C/U+201D) in a config value as curl 8.21.0 does: keep the bytes literally and write its warning, so the request matches the reference.

## Acceptance criteria

- [ ] `behaviour:test411`: Curl answers what curl 8.21.0 answers, `upstream test411 passes`, so the item measures `match`.
- [ ] `behaviour:test470`: Curl answers what curl 8.21.0 answers, `upstream test470 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
