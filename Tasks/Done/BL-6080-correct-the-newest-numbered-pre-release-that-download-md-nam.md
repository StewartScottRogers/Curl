---
id: BL-6080
title: Correct the newest numbered pre-release that DOWNLOAD.md names
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [DOWNLOAD.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6080 — Correct the newest numbered pre-release that DOWNLOAD.md names

## Goal

What `DOWNLOAD.md` says about the newest numbered pre-release is true and does not go stale at the next release.

## Context

`DOWNLOAD.md` ("Current version") names v0.2.0-preview.1 as the newest numbered pre-release, but later tags exist: ADR-1443 records v0.4.0-preview.3 and v0.4.0-preview.4, and BL-6079 records v0.4.0-preview.5 on nuget.org. Found during BL-6079, which did not touch `DOWNLOAD.md`.

## Acceptance criteria

- [x] `DOWNLOAD.md` names no older numbered pre-release as the newest one: it names the newest, or links the releases page without naming a version.

## Notes

- Linked the releases page without naming a version: a named version would go stale at the next release, which the goal rules out.
- Made directly rather than through `align-and-document`: a one-sentence change. Build clean (0 warnings) and fast tests green (115 test projects passed).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: d48cb4d3.
- 2026-11-27: Doing -> Done. DOWNLOAD.md links the releases page for numbered releases instead of naming a stale version
