---
id: BL-1824
title: Close GF-0031: Curl lists HTTP2 in Features; the Windows reference build does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1824 — Close GF-0031: Curl lists HTTP2 in Features; the Windows reference build does not

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0031 (Curl lists HTTP2 in Features; the Windows reference build does not), so a later gap analysis measures each of `features:HTTP2` as `match`.

## Context

- Finding: GF-0031, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: Medium. Introduced in: not stated upstream.
- Items: `features:HTTP2`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

features:HTTP2 expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks HTTP2; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific and drop HTTP2 from the Windows line, keeping it where the matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0141: HTTP/2 is hand-built and accepted everywhere, but offered by default only off Windows, so the Windows build does not offer it by default.

## Acceptance criteria

- [x] `features:HTTP2`: Curl answers what curl 8.21.0 answers, `not listed`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured `C:WindowsSystem32rl.exe -V` (8.21.0 Schannel): its Features line has no HTTP2. Dropped HTTP2 from `CurlVersionText.WindowsFeaturesLine` only; Linux and macOS keep it. No option changed, so `--ai-help` is unaffected. ADR-0450 records it. Delivered directly rather than the full /feature pipeline: a one-constant change, same shape as BL-1822 and BL-1823.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. curl -V on Windows no longer lists HTTP2, matching the Schannel reference build
