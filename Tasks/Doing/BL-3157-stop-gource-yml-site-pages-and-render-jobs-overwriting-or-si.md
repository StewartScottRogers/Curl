---
id: BL-3157
title: Stop gource.yml site-pages and render jobs overwriting or silently dropping the published agent pages
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/gource.yml]
requirement: none
created: 2026-10-22
completed:
---
# BL-3157 — Stop gource.yml site-pages and render jobs overwriting or silently dropping the published agent pages

## Goal

The published `llms.txt`, `download/`, `mcp/` and `ai-help/` pages on the `gource` branch are never replaced by older copies or dropped without a trace by `gource.yml`'s site-pages and render jobs.

## Context

Found by the shift-end review (ADR-0710) of 76d4c7a87..2f68618ca (BL-3148), by the code-reviewer and silent-failure-hunter agents; Important.

- `.github/workflows/gource.yml:456`: the `site-pages` job runs on `master` as well as `work/dark-factory`, copying the checked-out `llms.txt` and `.github/site/mcp/index.html` and committing when they differ. Master trails `work/dark-factory` by up to a shift, so master and its hourly scheduled runs put older pages back over newer ones - the problem BL-1206 fixed for `board-page`.
- `gource.yml:266-268`: the render job fetches each of these pages from raw.githubusercontent.com with `|| rm -f "$f"` and then force-pushes the `gource` branch, so any transient fetch failure (not only a 404) publishes the branch without them, with no warning.
- `gource.yml:479-490`: `curl -fsSL ... resources.json` and `ai-help.md` treat every failure (5xx, rate limit, DNS) as "not in the preview release yet" and the job stays green with `download/` or `ai-help/` unpublished.
- The site-pages publish clone (around `gource.yml:495`) exits at once on any clone failure with "no gource branch to publish onto", bypassing its own retry loop and blaming the wrong cause.

## Acceptance criteria

- [ ] `llms.txt` and `mcp/` are published only from `work/dark-factory` (master runs refresh at most the release-derived `download/` and `ai-help/`), with a comment saying why, as `board-page` has.
- [ ] The render job keeps a page it could not fetch for a reason other than 404 (or fails, or emits `::warning::`) rather than deleting it; a 404 still drops it quietly.
- [ ] Fetching `resources.json` or `ai-help.md` treats only HTTP 404 as "not yet"; any other failure emits `::warning::` or fails the step.
- [ ] The publish clone distinguishes a missing `gource` branch (`git ls-remote --exit-code`) from other clone failures, and retries the latter within its loop.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 4b79d485 73c92be4 73c1e8c0 ee88ffce.
