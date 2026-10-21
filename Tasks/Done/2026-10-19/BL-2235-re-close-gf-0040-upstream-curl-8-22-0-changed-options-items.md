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
completed: 2026-10-19
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

- [x] This task is obsolete, and closes without a change: GF-0040 is closed.

## Notes

- 2026-10-19: Stewart had it closed as obsolete. Its criteria were replaced by the one above while it was in the Backlog, and the evidence was checked mechanically first.

## Log

- 2026-10-14: Created.

- 2026-10-14 (Claude): nothing for a lane to do. The 8 --httpsig-* facets are now excluded as intended-difference:ADR-0485 on the gap branch (fd6b7eaf5, PR #142), so the next gap run re-measures them as excluded and closes GF-0040.
- 2026-10-14: Backlog -> Deferred. closes at the next gap run: --httpsig-* excluded as intended difference (fd6b7eaf5)
- 2026-10-19: Deferred -> Backlog. Stewart: close it as obsolete (GF-0040 is closed).
- 2026-10-19: Backlog -> Doing. Interactive session: closing it as obsolete. Criteria at claim: 35476782.
- 2026-10-19: Doing -> Done. Obsolete: GF-0040 is closed.
