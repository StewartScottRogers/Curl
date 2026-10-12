---
id: BL-2114
title: Close GF-0068: NETRC variable is not read; --netrc fails with exit 26 instead of using the named file
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-12
completed:
---
# BL-2114 — Close GF-0068: NETRC variable is not read; --netrc fails with exit 26 instead of using the named file

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0068 (NETRC variable is not read; --netrc fails with exit 26 instead of using the named file), so a later gap analysis measures each of `environment:NETRC` as `match`.

## Context

- Finding: GF-0068, filed by the gap analysis office (ADR-0433).
- Area: environment. Severity: High. Introduced in: not stated upstream.
- Items: `environment:NETRC`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/_ENVIRONMENT.md`, `docs/libcurl/libcurl-env.md`, `docs/cmdline-opts/config.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Recipe: NETRC=<folder>/gap-netrc (file holds 'machine 127.0.0.1 login gapuser password gappass'), 'curl -s --netrc http://127.0.0.1:<port>/'. Expected: exit 0; stdout ''; stderr ''; request GET / HTTP/1.1. Actual: exit 26; stdout ''; stderr ''; request none. A search of Curl.Cli.UnitLibrary/NetrcUse.cs finds no NETRC variable lookup. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-EnvironmentGap.ps1 -OutFile $env:TEMP\environment.json

Suggestion, copied from the finding:

Make the netrc file lookup behind --netrc (Curl.Cli.UnitLibrary NetrcUse, and the code that opens the file) read the NETRC environment variable and use the named file, as docs/cmdline-opts/_ENVIRONMENT.md and docs/libcurl/libcurl-env.md (NETRC) describe. Fall back to the home-directory .netrc (_netrc on Windows) only when NETRC is unset. Add unit tests with an injected environment.

## Acceptance criteria

- [ ] `environment:NETRC`: Curl answers what curl 8.21.0 answers, `exit 0; stdout ''; stderr ''; request GET / HTTP/1.1`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
