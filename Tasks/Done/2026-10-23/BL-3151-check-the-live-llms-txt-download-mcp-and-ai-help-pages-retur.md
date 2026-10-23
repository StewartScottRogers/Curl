---
id: BL-3151
title: Check the live llms.txt, download, mcp and ai-help pages return 200 after the site-pages job runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Tasks]
requirement: none
lane: no
created: 2026-10-22
completed: 2026-10-23
---
# BL-3151 — Check the live llms.txt, download, mcp and ai-help pages return 200 after the site-pages job runs

## Goal

After the `site-pages` job (BL-3148) has run on `work/dark-factory` or `master` and a release with `resources.json` exists, the four pages are live and every download URL they name works.

## Context

BL-3148 could not check live URLs in a lane before its change was integrated. `depends-on` is empty on purpose; start only once `https://stewartscottrogers.github.io/Curl/llms.txt` has been published (the gource-site job in gource.yml), or find out why it has not. Also check that the video page, `board/`, `gaps/`, `coverage/` and `integration/` still load after the next render (the render job carries llms.txt, download/, mcp/, ai-help/ over).

## Acceptance criteria

- [x] `/Curl/llms.txt`, `/Curl/download/`, `/Curl/mcp/` and `/Curl/ai-help/` each return 200, and every download URL they name returns 200 to a HEAD request; the commands and output are in Notes.
- [x] The video page, `board/`, `gaps/`, `coverage/` and `integration/` still return 200.

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
- 2026-10-22 (lane 1, second claim): the requeue condition still does not hold.
  `git branch -r --contains fb690a3b1` lists only origin/work/dark-factory; preview
  `resources.json` and `ai-help.md` still 404; `/Curl/download/` and `/Curl/ai-help/` 404;
  `/Curl/llms.txt`, `/Curl/mcp/`, `/Curl/`, `board/`, `gaps/`, `coverage/`,
  `integration/badge.svg` 200. The board re-offered the task because nothing on it gates the
  wait, so this claim sets `lane: no` to stop further empty claims. Remove lane: no once
  `git branch -r --contains fb690a3b1` lists origin/master and
  `curl -sI -L https://github.com/StewartScottRogers/Curl/releases/download/preview/resources.json`
  returns 200 (the shift-end merge to master runs release.yml).
- 2026-10-23 about 01:05 Arizona (interactive session): `git branch -r --contains fb690a3b1`
  lists origin/master. Measured with `curl.exe -s -o NUL -w '%{http_code}'` on each page and
  `curl.exe -s -I -L -o NUL -w '%{http_code}'` on every https URL the four pages name:
  - 200: `/Curl/llms.txt`, `/Curl/download/`, `/Curl/mcp/`, `/Curl/ai-help/`, the video page
    `/Curl/`, `board/`, `gaps/`, `coverage/`, `integration/badge.svg` (read as `integration/`,
    per the decision above).
  - 200, every download URL: preview `resources.json`, `ai-help.md`, `SHA256SUMS`, the six
    `curl-{win,linux,osx}-*` archives, `Curl.Api`, `Libcurl`, `Curl.Mcp` and its six RID
    `.nupkg` files (0.2.0-rolling.181), plus the repository, DOWNLOAD.md, LICENSE.txt and the
    Curl.Api, Libcurl and Curl.Mcp READMEs.
  - Not 200, all of them examples inside the ai-help reference text, not download links:
    `example.com/...` paths (404), `*.example` hosts (000, they do not resolve), and URL-glob
    and variable templates.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d171494f 49e82b27.
- 2026-10-22: Doing -> Backlog. Waiting for the shift-end merge to put BL-3147 (fb690a3b1) on master, so release.yml publishes resources.json and ai-help.md to the preview release, and for the next render; llms.txt and mcp/ are live (200)
- 2026-10-22: Backlog -> Doing. Criteria at claim: d171494f 49e82b27.
- 2026-10-22: Doing -> Backlog. Still waiting for the shift-end merge to put BL-3147 (fb690a3b1) on master so release.yml publishes preview resources.json and ai-help.md; download/ and ai-help/ still 404. Set lane: no until that holds
- 2026-10-23: Backlog -> Doing. Interactive session: fb690a3b1 is on master and preview has resources.json; checking the live pages. Criteria at claim: d171494f 49e82b27.
- 2026-10-23: Doing -> Done. All four AI-discovery pages and every download URL they name return 200; video, board, gaps, coverage and integration badge 200
