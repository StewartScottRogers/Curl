---
id: BL-2329
title: Close GF-0086: Curl lists ECH in Features: on Linux; the curl 8.22.0 OpenSSL reference does not (on linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2329 — Close GF-0086: Curl lists ECH in Features: on Linux; the curl 8.22.0 OpenSSL reference does not (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0086 (Curl lists ECH in Features: on Linux; the curl 8.22.0 OpenSSL reference does not), so a later gap analysis measures each of `features:ECH` as `match`.

## Context

- Finding: GF-0086, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: Medium. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `features:ECH`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measurement features:ECH: expected 'not listed', actual 'listed', state gap. Reference is curl 8.22.0 (x86_64-pc-linux-gnu) built with OpenSSL/3.0.13, per the measurement's reference field. Reproduce on the Linux runner: curl -V | grep '^Features:' (the item's evidence). ECH is absent from the reference line and present in Curl's FeaturesLine.

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary, CurlVersionText.FeaturesLine (CurlVersionText.cs, line 51), drop ECH so the Linux and macOS Features: line stops listing it. Keep --ech and the hand-built EchClientHello and EchConfigList in Curl.Tls.UnitLibrary working; only the line changes. FeaturesLine is shared by Linux and macOS, so the macOS measurement must agree before the change lands. Update the pinning test in Curl.Cli.UnitTests in the same change (ADR-0021 Decision 6). explained by ADR-0448: that ADR drops ECH from the Windows line only and keeps it on Linux and macOS, on the premise that the OpenSSL build lists it; this measurement shows the 8.22.0 OpenSSL reference does not, so the Linux half needs a superseding ADR. The ADR does not remove this group.

## Acceptance criteria

- [x] `features:ECH`: Curl answers what curl 8.22.0 answers on linux, `not listed`, so the item measures `match`.
- [x] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Decision (ADR-0520): `FeaturesLine` drops `ECH` on Linux and macOS too. curl builds ECH only with `--enable-ech` (experimental, needs an ECH-capable TLS library); the gap-platforms reference is a default tarball build on both Linux and macOS, so neither lists it and the shared line needs no platform split. `--ech` keeps working. ADR-0448 marked superseded for its Linux/macOS half.
- Proof on Linux: `CurlVersionTextTests` pins `Lines(isWindows: false, ...)` platform-neutrally, so the CI Linux and macOS jobs check the line once the shift pushes; the gap-platforms run re-measures `features:ECH` (a lane cannot dispatch it).
- Only a string constant and doc comments changed, no method, so no complexity or coverage change; Measure-CodeQuality.ps1 not run for that reason.
- `--ai-help`: no option added or changed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. curl -V on Linux and macOS no longer lists ECH, matching the curl 8.22.0 OpenSSL reference (ADR-0520)
