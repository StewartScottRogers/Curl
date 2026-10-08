---
id: BL-1751
title: Confirm the Gource run's gaps-page job succeeded and Pages serves Curl/gaps/
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1743]
touches: [.github/workflows/gource.yml]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1751 — Confirm the Gource run's gaps-page job succeeded and Pages serves Curl/gaps/

## Goal

The `gaps-page` job BL-1743 added to `.github/workflows/gource.yml` is shown working on
GitHub, and the gap dashboard is live at `https://stewartscottrogers.github.io/Curl/gaps/`.

## Context

BL-1743 wrote the job and the render job's `gaps/` carry-over on a dark factory lane. A lane
cannot push, so it could not check the job's first run. This task is that check, made once
the shift has pushed BL-1743's commit to `work/dark-factory`. If the job failed, fix it in
`gource.yml` (read its log with `gh run view <id> --log-failed`).

## Acceptance criteria

- [x] `gh run list --workflow Gource --branch work/dark-factory --limit 5` shows a run on or after BL-1743's commit whose `gaps-page` job is `success` (`gh run view <id> --json jobs`); the run ID goes in Notes.
- [x] `Invoke-WebRequest https://stewartscottrogers.github.io/Curl/gaps/` returns status 200 with the dashboard's HTML once Pages has rebuilt; the result goes in Notes.

## Notes

- Gource run 37820053796 (BL-1743's commit 476b10a78): `gaps-page` success, and it made
  the first publish of `gaps/index.html` to `gource` (17:53:48Z). Runs 37820075784
  (4c96e63f8) and 37820241256 (a3a28a569) also show `gaps-page` success. The runs read
  `cancelled` overall only because later pushes cancelled their `decide` job.
- A render that was already in flight force-pushed `gource` at 17:54:27Z from a tree
  fetched before the first publish, so `gaps/` was dropped once; the next push's
  `gaps-page` (run 37820241256) saw it missing and republished it at 17:55:09Z. From now
  on the render job carries `gaps/` over from the branch, so this was a one-time race on
  the very first publish and needs no change to `gource.yml`.
- Pages build of 1de424d45 finished `built` at 17:57Z; `Invoke-WebRequest
  https://stewartscottrogers.github.io/Curl/gaps/` returned 200, 16708 bytes,
  `<title>Curl gap dashboard</title>`.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. gaps-page job green (run 37820053796) and Pages serves Curl/gaps/ with 200
