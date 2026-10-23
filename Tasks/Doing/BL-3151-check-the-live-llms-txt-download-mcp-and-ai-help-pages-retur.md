---
id: BL-3151
title: Check the live llms.txt, download, mcp and ai-help pages return 200 after the site-pages job runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Tasks]
requirement: none
created: 2026-10-22
completed:
---
# BL-3151 — Check the live llms.txt, download, mcp and ai-help pages return 200 after the site-pages job runs

## Goal

After the `site-pages` job (BL-3148) has run on `work/dark-factory` or `master` and a release with `resources.json` exists, the four pages are live and every download URL they name works.

## Context

BL-3148 could not check live URLs in a lane before its change was integrated. `depends-on` is empty on purpose; start only once `https://stewartscottrogers.github.io/Curl/llms.txt` has been published (the gource-site job in gource.yml), or find out why it has not. Also check that the video page, `board/`, `gaps/`, `coverage/` and `integration/` still load after the next render (the render job carries llms.txt, download/, mcp/, ai-help/ over).

## Acceptance criteria

- [ ] `/Curl/llms.txt`, `/Curl/download/`, `/Curl/mcp/` and `/Curl/ai-help/` each return 200, and every download URL they name returns 200 to a HEAD request; the commands and output are in Notes.
- [ ] The video page, `board/`, `gaps/`, `coverage/` and `integration/` still return 200.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d171494f 49e82b27.
