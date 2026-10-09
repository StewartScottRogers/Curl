---
id: BL-1827
title: Close GF-0034: Curl does not list SSPI in Features; the Windows reference build does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1827 — Close GF-0034: Curl does not list SSPI in Features; the Windows reference build does

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0034 (Curl does not list SSPI in Features; the Windows reference build does), so a later gap analysis measures each of `features:SSPI` as `match`.

## Context

- Finding: GF-0034, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: High. Introduced in: not stated upstream.
- Items: `features:SSPI`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

features:SSPI expected listed, actual not listed (curl -V Features: line). The reference Features: line lists SSPI; Curl's CurlVersionText.FeaturesLine does not. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary/CurlVersionText.cs, add SSPI to the Windows FeaturesLine only once Curl's Windows NTLM and Negotiate path actually answers through SSPI, and pin it in CurlVersionTextTests in Curl.Cli.UnitTests. ADR-0142 says that path already runs through SSPI on Windows, so verify it before listing. ADR-0021 says the opposite (Curl calls no SSPI on any platform), so resolve that conflict in an ADR first.

## Acceptance criteria

- [ ] `features:SSPI`: Curl answers what curl 8.21.0 answers, `listed`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
