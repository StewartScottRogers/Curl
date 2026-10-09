---
id: BL-1825
title: Close GF-0032: Curl lists HTTP3 in Features; the Windows reference build does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1825 — Close GF-0032: Curl lists HTTP3 in Features; the Windows reference build does not

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0032 (Curl lists HTTP3 in Features; the Windows reference build does not), so a later gap analysis measures each of `features:HTTP3` as `match`.

## Context

- Finding: GF-0032, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: Medium. Introduced in: not stated upstream.
- Items: `features:HTTP3`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

features:HTTP3 expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks HTTP3; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific and drop HTTP3 from the Windows line, keeping it where the matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0144: HTTP/3 is hand-built over a hand-built QUIC, which the Schannel reference build does not offer; ADR-0140 notes QUIC runs TCP-only where SslStream cannot.

## Acceptance criteria

- [ ] `features:HTTP3`: Curl answers what curl 8.21.0 answers, `not listed`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
