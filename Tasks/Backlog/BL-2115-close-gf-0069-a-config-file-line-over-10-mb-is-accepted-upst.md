---
id: BL-2115
title: Close GF-0069: A config-file line over 10 MB is accepted; upstream rejects the file with 'cannot read config'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-12
completed:
---
# BL-2115 — Close GF-0069: A config-file line over 10 MB is accepted; upstream rejects the file with 'cannot read config'

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0069 (A config-file line over 10 MB is accepted; upstream rejects the file with 'cannot read config'), so a later gap analysis measures each of `environment:config-syntax:line-length-limit` as `match`.

## Context

- Finding: GF-0069, filed by the gap analysis office (ADR-0433).
- Area: environment. Severity: Medium. Introduced in: not stated upstream.
- Items: `environment:config-syntax:line-length-limit`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/_ENVIRONMENT.md`, `docs/libcurl/libcurl-env.md`, `docs/cmdline-opts/config.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Recipe: a .curlrc with a comment line one byte over 10 MB, then 'write-out = gap-config-hit'. Expected: exit 0; stdout ''; stderr 'curl: cannot read config from ...' (the reference prints the path wrapped across lines). Actual: exit 0; stdout 'gap-config-hit'; stderr ''. A search of Curl.Cli.UnitLibrary/ConfigFileSyntax.cs finds no line-length limit. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-EnvironmentGap.ps1 -OutFile $env:TEMP\environment.json

Suggestion, copied from the finding:

Add the 10 MB (10485760-byte) per-line limit that docs/cmdline-opts/config.md states ('no more than 10 megabytes') to the config reader in Curl.Cli.UnitLibrary (ConfigFileSyntax / ConfigFileApplier). When a line exceeds it, stop reading the file and print 'curl: cannot read config from <path>' as the reference does. Measure the reference's exact stderr text and exit code first.

## Acceptance criteria

- [ ] `environment:config-syntax:line-length-limit`: Curl answers what curl 8.21.0 answers, `exit 0; stdout ''; stderr 'curl: cannot read config from 'C:\Users\Stewart 
curl: Rogers\AppData\Local\Temp\gap-env-6a4d2efcfdf041709bfc486fb8998815\.curlr
curl: c''; request none`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-12: Created.
