---
id: BL-1823
title: Close GF-0030: Curl lists GSS-API in Features; the Windows reference build does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1823 — Close GF-0030: Curl lists GSS-API in Features; the Windows reference build does not

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0030 (Curl lists GSS-API in Features; the Windows reference build does not), so a later gap analysis measures each of `features:GSS-API` as `match`.

## Context

- Finding: GF-0030, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: Medium. Introduced in: not stated upstream.
- Items: `features:GSS-API`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

features:GSS-API expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks GSS-API; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary/CurlVersionText.cs, drop GSS-API from the Windows FeaturesLine, as ADR-0021 requires for names the platform's curl does not list, and keep it on platforms whose matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0171: Curl hand-builds the GSS-API Kerberos initiator; and by ADR-0142: on Windows Negotiate answers through SSPI, so the Windows build does not use GSS-API.

## Acceptance criteria

- [ ] `features:GSS-API`: Curl answers what curl 8.21.0 answers, `not listed`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
