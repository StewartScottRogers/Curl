---
id: BL-757
title: Draw the task board as a live kanban page from one GitHub tree call
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-762]
touches: [.github/board]
requirement: none
created: 2026-09-28
completed:
---
# BL-757 — Draw the task board as a live kanban page from one GitHub tree call

## Goal

`.github/board/site/index.html` is a single static page. In the viewer's browser it reads the recursive git tree of `work/dark-factory` from the GitHub REST API and draws `Tasks/` as five kanban columns, Backlog, Doing, Blocked, Deferred and Done. Each card shows a task's ID and title and links to the task's file on GitHub. The page refreshes itself every 60 seconds and has a place ready for lane cards (BL-761).

## Context

- Stewart chose this on 2026-09-28 (option 1). The data sources, title rule, refresh budget, query overrides and file location are fixed by the ADR from BL-762; read it first and follow it.
- **Pipeline:** `direct`, because this is a static web page with no .NET project.
- **Tooling:** no build step, no package, no framework, and no external script, stylesheet or font. Use plain HTML, CSS and JavaScript (`fetch`, `setTimeout`, DOM APIs). Inline the CSS and JS in `index.html`.
- **API call:** `GET https://api.github.com/repos/StewartScottRogers/Curl/git/trees/work/dark-factory?recursive=1`. It answers with CORS open and a limit of 60 calls an hour, and is about 1.4 MB (measured 2026-09-28, see the ADR).
- **Card paths:** keep only `Tasks/<State>/BL-*.md` and `Tasks/Done/<yyyy-MM-dd_HHmm>/BL-*.md`. `Tasks/*/README.md`, `Tasks/Tasks.projitems` and everything outside `Tasks/` are not cards.
- **Card titles:** file names are `BL-###-<slug>.md`, and `task-board.ps1` lines 362-363 make the slug. Derive the title as the ADR says: hyphens become spaces, the first letter is upper-cased, and `…` is appended when the slug is 59 or 60 characters. Order cards by ID within a column. When the same ID appears twice (parallel lanes can file one before `dedupe` runs), show both cards.
- **Room for BL-761:** give the lane cards somewhere to go without a rewrite. Put an empty `<section id="lanes">` above the columns. Structure a refresh as a list of data sources, each an object with a `fetch` step and a `render` step, where the tree is the first source. The refresh interval is 60 s times the number of sources, so adding a source is one array entry.
- **Local preview:** `dotnet run --file .github/gource/serve.cs -- .github/board 8000` serves the folder, so the page is at `http://localhost:8000/site/index.html`. `serve.cs` already maps `.html` and `.json`.
- **Headless check:** Microsoft Edge is at `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`. `msedge --headless=new --disable-gpu --virtual-time-budget=10000 --dump-dom "<url>"` prints the DOM after scripts run.

## Acceptance criteria

- [ ] `.github/board/site/index.html` exists. `Select-String -Path .github/board/site/index.html -Pattern '<script[^>]+src=|<link[^>]+href=|@import|import\s'` finds nothing, and the only URL the script requests by default is under `https://api.github.com/`.
- [ ] `.github/board/fixtures/tree.json` is a trimmed tree response shaped like the API's (`sha`, `tree[]` with `path`/`type`, `truncated`). It contains at least:
  - two tasks in each of Backlog, Doing, Blocked, Deferred and Done;
  - two archive folders under `Tasks/Done/` holding 3 and 2 tasks;
  - a `Tasks/Backlog/README.md`;
  - a path outside `Tasks/`;
  - one task whose slug is exactly 60 characters.
- [ ] Serving `.github/board` with `serve.cs` and dumping `http://localhost:8000/site/index.html?tree=../fixtures/tree.json` with headless Edge gives a DOM with:
  - five columns titled Backlog, Doing, Blocked, Deferred and Done, each showing its card count;
  - every fixture task as a card whose text has its `BL-###` ID and derived title, and whose link is `https://github.com/StewartScottRogers/Curl/blob/work/dark-factory/<path>`;
  - the 60-character slug's title ending in `…`;
  - Done ending with one collapsed `<details>` row per archive folder whose summary shows the folder name and `3 tasks` or `2 tasks`;
  - no card for the README or the outside path;
  - an empty `section#lanes`.
- [ ] With a fixture copy whose `truncated` is `true`, the dumped DOM shows a visible warning that the tree was truncated.
- [ ] The page shows the time of the last successful refresh, and schedules the next one with `setTimeout` at 60 s times the number of data sources. `?refresh=30` is clamped up to that minimum and `?refresh=300` is honoured; this is visible in the code and in a line of the page footer.
- [ ] When the tree call answers 403 or 429, or `X-RateLimit-Remaining` is `0`, the page shows "GitHub's hourly API limit is used up; next refresh at HH:MM", using `X-RateLimit-Reset`, and waits until then. A failed fetch keeps the last board on screen with an error line.
- [ ] `?repo=` and `?branch=` change the API URL and the card links. The comment at the top of the file documents every query override and how to add a data source.
- [ ] Against the live API, `http://localhost:8000/site/index.html` served locally draws the real board. Compare its Backlog count with the `BL-*.md` count from `git ls-tree --name-only origin/work/dark-factory Tasks/Backlog/`; any difference must come from moves pushed after that ref was last fetched. Record both numbers in Notes.

## Notes

## Log

- 2026-09-28: Created.
