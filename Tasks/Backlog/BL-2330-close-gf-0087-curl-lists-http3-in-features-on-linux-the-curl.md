---
id: BL-2330
title: Close GF-0087: Curl lists HTTP3 in Features: on Linux; the curl 8.22.0 OpenSSL reference does not (on linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2330 — Close GF-0087: Curl lists HTTP3 in Features: on Linux; the curl 8.22.0 OpenSSL reference does not (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0087 (Curl lists HTTP3 in Features: on Linux; the curl 8.22.0 OpenSSL reference does not), so a later gap analysis measures each of `features:HTTP3` as `match`.

## Context

- Finding: GF-0087, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: Medium. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `features:HTTP3`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measurement features:HTTP3: expected 'not listed', actual 'listed', state gap. Reference is curl 8.22.0 built with OpenSSL/3.0.13 and no QUIC library (no ngtcp2, quiche or OpenSSL QUIC in its version line). Reproduce on the Linux runner: curl -V | grep '^Features:' (the item's evidence).

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary, CurlVersionText.FeaturesLine (CurlVersionText.cs, line 51), drop HTTP3 so the Linux and macOS Features: line stops listing it. Keep --http3, --http3-only and the hand-built HTTP/3 and QUIC stack working; only the line changes. Update the pinning test in Curl.Cli.UnitTests in the same change. Explained by ADR-0451: it drops HTTP3 from the Windows line only and keeps it on Linux and macOS under ADR-0144 and ADR-0021; this measurement shows the 8.22.0 OpenSSL reference does not list it, so the Linux half needs a superseding ADR. The ADR does not remove this group.

## Acceptance criteria

- [ ] `features:HTTP3`: Curl answers what curl 8.22.0 answers on linux, `not listed`, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
