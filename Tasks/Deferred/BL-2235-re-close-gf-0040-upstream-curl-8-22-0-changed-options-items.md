---
id: BL-2235
title: Re-close GF-0040: Upstream curl 8.22.0 changed options items
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-14
completed:
---
# BL-2235 — Re-close GF-0040: Upstream curl 8.22.0 changed options items

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0040 (Upstream curl 8.22.0 changed options items), so a later gap analysis measures each of `options:--httpsig-algo`, `options:--httpsig-headers`, `options:--httpsig-key`, `options:--httpsig-keyid`, `options:--httpsig-algo:argument`, `options:--httpsig-headers:argument`, `options:--httpsig-key:argument`, `options:--httpsig-keyid:argument` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0040 ([BL-2171]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0040, filed by the gap analysis office (ADR-0433).
- Area: options. Severity: High. Introduced in: 8.22.0.
- Items: `options:--httpsig-algo`, `options:--httpsig-headers`, `options:--httpsig-key`, `options:--httpsig-keyid`, `options:--httpsig-algo:argument`, `options:--httpsig-headers:argument`, `options:--httpsig-key:argument`, `options:--httpsig-keyid:argument`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/*.md` in the curl 8.22.0 release tarball.
- Touches: none. The finding named no Curl project outside the office, so this task has no touches and runs alone.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

- options:--httpsig-algo: added in 8.22.0.

- options:--httpsig-headers: added in 8.22.0.

- options:--httpsig-key: added in 8.22.0.

- options:--httpsig-keyid: added in 8.22.0.

Suggestion, copied from the finding:

Bring these items to Curl when the target moves to 8.22.0.

## Acceptance criteria

- [ ] `options:--httpsig-algo`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --httpsig-algo: is unknown | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--httpsig-headers`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --httpsig-headers: is unknown | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--httpsig-key`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --httpsig-key: is unknown | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--httpsig-keyid`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --httpsig-keyid: is unknown | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--httpsig-algo:argument`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --httpsig-algo: is unknown | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--httpsig-headers:argument`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --httpsig-headers: is unknown | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--httpsig-key:argument`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --httpsig-key: is unknown | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `options:--httpsig-keyid:argument`: Curl answers what curl 8.22.0 answers, `exit 2: curl: option --httpsig-keyid: is unknown | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-14: Created.

- 2026-10-14 (Claude): nothing for a lane to do. The 8 --httpsig-* facets are now excluded as intended-difference:ADR-0485 on the gap branch (fd6b7eaf5, PR #142), so the next gap run re-measures them as excluded and closes GF-0040.
- 2026-10-14: Backlog -> Deferred. closes at the next gap run: --httpsig-* excluded as intended difference (fd6b7eaf5)
