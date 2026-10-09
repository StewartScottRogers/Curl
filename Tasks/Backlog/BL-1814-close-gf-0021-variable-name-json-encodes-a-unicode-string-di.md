---
id: BL-1814
title: Close GF-0021: --variable ... {{name:json}} encodes a Unicode string differently (stderr)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1814 — Close GF-0021: --variable ... {{name:json}} encodes a Unicode string differently (stderr)

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0021 (--variable ... {{name:json}} encodes a Unicode string differently (stderr)), so a later gap analysis measures each of `behaviour:test268` as `match`.

## Context

- Finding: GF-0021, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Medium. Introduced in: not stated upstream.
- Items: `behaviour:test268`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test268 expected 'upstream test268 passes' (the reference curl exits 0), actual 'stderr differs' from the reference curl. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 268

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's --variable / --expand-* expansion, make the ':json' function escape a Unicode string byte for byte as curl 8.21.0 does: control characters as \uXXXX, and bytes >= 0x80 passed through. Make the stderr match the reference for upstream tests/data/test268.

## Acceptance criteria

- [ ] `behaviour:test268`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
