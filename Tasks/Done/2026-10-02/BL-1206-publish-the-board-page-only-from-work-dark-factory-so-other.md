---
id: BL-1206
title: Publish the board page only from work/dark-factory so other branches stop restoring an old copy
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/gource.yml]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1206 — Publish the board page only from work/dark-factory so other branches stop restoring an old copy

## Goal

Only pushes to `work/dark-factory` publish the live board page, so the page Stewart sees is always the factory's newest one.

## Context

- 2026-10-02: BL-1184 and BL-1185 (the Tasks and Audit tabs) landed on `work/dark-factory` at 10:46 and 11:01, but the published page at https://stewartscottrogers.github.io/Curl/board/ had no Audit tab. `gource.yml`'s `board-page` job ran on every push to every branch and on the hourly schedule (which runs on `master`). Each published its own branch's copy of `.github/board/site/index.html`, so the push of `audit` for PR #48 at 19:01 UTC put `master`'s older page back.

## Acceptance criteria

- [x] `board-page` runs only when `github.ref` is `refs/heads/work/dark-factory`; pushes to `master`, `audit` and the schedule skip it.
- [x] The workflow header's description of `board-page` says so.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-02: The push of this commit to `work/dark-factory` runs `board-page`, which republishes the page with the Audit tab.
## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Only work/dark-factory publishes the board page
