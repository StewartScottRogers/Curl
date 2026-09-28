---
id: BL-758
title: Publish the live task board page on GitHub Pages and link it from the README
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-757, BL-756]
touches: [.github/workflows/gource.yml, README.md, .claude/agents/showcase-publisher.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-758 — Publish the live task board page on GitHub Pages and link it from the README

## Goal

The live task board is served at https://stewartscottrogers.github.io/Curl/board/. It is kept there by `.github/workflows/gource.yml`, which publishes it within minutes of a change to `.github/board/site/index.html` and does not wait for an 8K render. The GitHub README links to it next to the showcase.

## Context

- Stewart asked for a README link to the board near the showcase on 2026-09-28. The page is `.github/board/site/index.html` from BL-757, and where it is published is fixed by the ADR from BL-755.
- **Where Pages serves from:** the `gource` branch. `gource.yml` writes that branch two ways:
  - The `coverage` job commits `coverage/` onto it without force: a sparse, blob-less clone, a retry loop, then `gh api -X POST repos/$GITHUB_REPOSITORY/pages/builds`.
  - The `render` job force-pushes a fresh single-commit branch. It copies `.github/gource/site/index.html` from the workspace and carries `coverage/` over by downloading its files from `raw.githubusercontent.com`.
  - Both are gated on `decide`, so without a new job the page would appear only when a render happens.
- **Why a render needs a fallback:** `schedule` runs use `master`'s copy of the workflow and workspace, and `master` has no `.github/board/` until the next shift-end merge. So a render must carry `board/index.html` over from the current `gource` branch when the workspace lacks the source, the same way it carries `coverage/`.
- **Ownership:** the `showcase-publisher` agent owns this workflow and the `gource` branch. Its definition, `.claude/agents/showcase-publisher.md`, should list `board/` as part of what the branch holds.
- **README:** in `README.md`, the Gource GIF is on line 3 and the coverage badge on line 14.
- **Force push:** the render job's force push to `gource` is already a standing exception in `CLAUDE.md`, and nothing here adds another.

## Acceptance criteria

- [ ] `gource.yml` has a new job, e.g. `board-page`, that runs on every trigger and is not gated on `decide`.
  - When `.github/board/site/index.html` exists in the workspace and differs from the `gource` branch's `board/index.html`, it commits the file onto `gource` as `board/index.html` without force, using the same sparse clone and three-try loop as the coverage job. It then requests a Pages build.
  - When the file is identical or missing, the job does nothing and succeeds.
- [ ] The `render` job copies `.github/board/site/index.html` to `board/index.html` when the workspace has it, and otherwise downloads `board/index.html` from the current `gource` branch as it does `coverage/`. `board` is included in its `git add` only when the file exists.
- [ ] The README text that the render job writes onto the branch mentions the board page, and the workflow's header comment describes the new job and `board/`.
- [ ] `README.md` has a link next to the coverage badge, reading "Live task board" or similar, to `https://stewartscottrogers.github.io/Curl/board/`. It has one sentence saying the board shows the tasks by state and the dark factory's lanes, and refreshes itself.
- [ ] `.claude/agents/showcase-publisher.md` lists `board/index.html` among the `gource` branch's contents and says where its source lives.
- [ ] Nothing in the workflow force-pushes anything other than `gource`.

## Notes

- A lane cannot see the workflow run: runs do not push, and lanes are denied `git push`/`fetch`. Whoever next looks at Actions after this lands should confirm that the `board-page` job succeeded and that `https://stewartscottrogers.github.io/Curl/board/` answers 200. If it does not, file a task.

## Log

- 2026-09-28: Created.
