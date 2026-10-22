---
id: BL-1826
title: Close GF-0033: Curl lists TLS-SRP in Features; the Windows reference build does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1826 — Close GF-0033: Curl lists TLS-SRP in Features; the Windows reference build does not

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0033 (Curl lists TLS-SRP in Features; the Windows reference build does not), so a later gap analysis measures each of `features:TLS-SRP` as `match`.

## Context

- Finding: GF-0033, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: Medium. Introduced in: not stated upstream.
- Items: `features:TLS-SRP`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

features:TLS-SRP expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks TLS-SRP; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific and drop TLS-SRP from the Windows line, keeping it where the matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0328 and ADR-0229: Curl runs TLS-SRP through its hand-built TLS 1.2 client as the OpenSSL build does; the Schannel reference does not offer it.

## Acceptance criteria

- [x] `features:TLS-SRP`: Curl answers what curl 8.21.0 answers, `not listed`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured `C:WindowsSystem32rl.exe -V` (Schannel): its Features line has no TLS-SRP. Dropped TLS-SRP from `CurlVersionText.WindowsFeaturesLine` only; Linux and macOS keep it, and the TLS-SRP options still work everywhere. ADR-0452, following ADR-0450 and ADR-0451. No option changed, so `--ai-help` needs no change. Pipeline was `feature`, but the change is one constant, so it was made directly as BL-1825 was.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. curl -V on Windows omits TLS-SRP from Features, as the Schannel reference build does (ADR-0452)
