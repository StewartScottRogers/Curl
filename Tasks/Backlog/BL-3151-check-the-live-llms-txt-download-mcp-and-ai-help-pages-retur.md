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

- 2026-10-23 00:10Z (lane 1), measured with `curl -s -o /dev/null -w '%{http_code}'` (HEAD with
  `-I -L` for the URLs the pages name), after Gource run 39876301105's site-pages job (success)
  and Pages deploy 39876324019 (success):
  - 200: `/Curl/llms.txt`, `/Curl/mcp/`, the video page `/Curl/`, `board/`, `gaps/`, `coverage/`,
    `integration/badge.svg`, `integration/linux.json`; and from llms.txt and mcp/: the repository,
    `blob/master/DOWNLOAD.md`, `LICENSE.txt`, `Curl.Mcp/README.md`, `releases/download/preview/SHA256SUMS`.
  - 404: `/Curl/download/`, `/Curl/ai-help/`, `releases/download/preview/resources.json`,
    `releases/download/preview/ai-help.md`, `integration/`.
- Why download/ and ai-help/ are missing: the `preview` release has no resources.json or
  ai-help.md yet. release.yml rebuilds `preview` only on a push to master, and BL-3147's
  commit fb690a3b1 is on work/dark-factory but not yet on master. site-pages leaves out
  both pages while the release lacks those files, so they appear after the next shift-end
  merge to master has run release.yml. If site-pages on that master push finishes before
  release.yml uploads them, the next hourly scheduled run (from master) or the next push to
  work/dark-factory publishes them, so no workflow change is needed.
- `integration/` has never had an index page: it holds only `badge.svg` and the per-platform
  JSON files integration.yml writes, which the README links to. Decision (sensible default):
  read "integration/ still returns 200" as `integration/badge.svg` returning 200.
- The render job has not run since BL-3148 (rendered-at.txt 1792707678 predates it; newer
  pushes cancelled the runs since), so criterion 2's "after the next render" cannot be
  checked yet either.
- Requeue: claim this again only once `git branch -r --contains fb690a3b1` lists origin/master
  and `curl -sI -L https://github.com/StewartScottRogers/Curl/releases/download/preview/resources.json`
  returns 200. Then re-run the commands above; that takes about 5 turns.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d171494f 49e82b27.
- 2026-10-22: Doing -> Backlog. Waiting for the shift-end merge to put BL-3147 (fb690a3b1) on master, so release.yml publishes resources.json and ai-help.md to the preview release, and for the next render; llms.txt and mcp/ are live (200)
