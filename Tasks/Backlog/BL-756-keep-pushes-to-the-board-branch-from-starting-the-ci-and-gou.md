---
id: BL-756
title: Keep pushes to the board branch from starting the CI and Gource workflows
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/ci.yml, .github/workflows/gource.yml]
requirement: none
created: 2026-09-28
completed:
---
# BL-756 — Keep pushes to the board branch from starting the CI and Gource workflows

## Goal

A push to the `board` branch starts no workflow, and the Gource workflow does not count it as new work. This has to hold before the dark factory starts force-pushing lane heartbeats there every few minutes (BL-760).

## Context

- Stewart chose lane heartbeats on a dedicated, force-pushed `board` branch on 2026-09-28 (option 2; see BL-762 for the contract). The coordinator will push it about every 3 minutes during a shift.
- `.github/workflows/ci.yml` triggers on `push` with `branches-ignore: [gource, 'factory/**']`. Without a change, every heartbeat would start a Windows, Linux and macOS build.
- `.github/workflows/gource.yml` triggers on `push` with `branches-ignore: [gource]`. Its `decide` job fingerprints every remote ref except `gource` and `HEAD`, using the `grep -v -E '^refs/remotes/origin/(gource|HEAD) '` filter. A changing `board` ref would therefore look like new work, and the workflow would re-render the 8K video every 30 minutes.
- `.github/workflows/release.yml` triggers only on `v*` tags and on pull requests, so it needs no change.
- The `showcase-publisher` agent owns `gource.yml`; keep its header comment accurate.

## Acceptance criteria

- [ ] `ci.yml`'s `push.branches-ignore` lists `board` beside `gource` and `'factory/**'`, and its comment says why.
- [ ] `gource.yml`'s `push.branches-ignore` lists `board`.
- [ ] The `decide` job's fingerprint filter excludes `refs/remotes/origin/board` as it excludes `gource`, i.e. the pattern reads `^refs/remotes/origin/(gource|board|HEAD) `.
- [ ] `gource.yml`'s header comment ("When it renders") says that pushes to `board` are ignored and do not count as new commits.
- [ ] `git diff` of the task touches only those two files, and nothing else in either workflow changes.

## Notes

## Log

- 2026-09-28: Created.
