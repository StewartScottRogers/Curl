---
id: BL-1828
title: Close GF-0035: Curl does not list threadsafe in Features; the Windows reference build does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1828 — Close GF-0035: Curl does not list threadsafe in Features; the Windows reference build does

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0035 (Curl does not list threadsafe in Features; the Windows reference build does), so a later gap analysis measures each of `features:threadsafe` as `match`.

## Context

- Finding: GF-0035, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: High. Introduced in: not stated upstream.
- Items: `features:threadsafe`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

features:threadsafe expected listed, actual not listed (curl -V Features: line). The reference Features: line lists threadsafe; Curl's CurlVersionText.FeaturesLine does not. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

Suggestion, copied from the finding:

Record in an ADR whether Curl's transfers are thread-safe in the sense curl means. If it is decided yes, add threadsafe to the FeaturesLine in Curl.Cli.UnitLibrary/CurlVersionText.cs and pin it in CurlVersionTextTests in Curl.Cli.UnitTests. ADR-0021 excludes it because Curl exposes no libcurl API for global initialisation to be thread-safe of, so the ADR must settle that first.

## Acceptance criteria

- [ ] `features:threadsafe`: Curl answers what curl 8.21.0 answers, `listed`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
