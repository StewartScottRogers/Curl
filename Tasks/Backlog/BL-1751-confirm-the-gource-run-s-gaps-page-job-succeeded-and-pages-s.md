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
completed:
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

- [ ] `gh run list --workflow Gource --branch work/dark-factory --limit 5` shows a run on or after BL-1743's commit whose `gaps-page` job is `success` (`gh run view <id> --json jobs`); the run ID goes in Notes.
- [ ] `Invoke-WebRequest https://stewartscottrogers.github.io/Curl/gaps/` returns status 200 with the dashboard's HTML once Pages has rebuilt; the result goes in Notes.

## Notes

## Log

- 2026-10-08: Created.
