---
id: BL-1822
title: Close GF-0029: Curl lists ECH in Features; the Windows reference build does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1822 — Close GF-0029: Curl lists ECH in Features; the Windows reference build does not

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0029 (Curl lists ECH in Features; the Windows reference build does not), so a later gap analysis measures each of `features:ECH` as `match`.

## Context

- Finding: GF-0029, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: Medium. Introduced in: not stated upstream.
- Items: `features:ECH`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

features:ECH expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks ECH; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific (Lines already branches on isWindows and isMacOS) and remove ECH from the Windows line, keeping it only where the matched reference lists it. Pin each platform's line in CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0327 and ADR-0233: Curl hand-builds ECH in its TLS 1.3 client, which the Schannel reference does not offer; ADR-0021 says list only what Curl implements, so the line must also match the reference per platform.

## Acceptance criteria

- [ ] `features:ECH`: Curl answers what curl 8.21.0 answers, `not listed`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
