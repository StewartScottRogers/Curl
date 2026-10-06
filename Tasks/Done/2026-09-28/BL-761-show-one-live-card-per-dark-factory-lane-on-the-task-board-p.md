---
id: BL-761
title: Show one live card per dark factory lane on the task board page, flagging stale heartbeats
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-757, BL-762]
touches: [.github/board]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-761 — Show one live card per dark factory lane on the task board page, flagging stale heartbeats

## Goal

The live task board page reads `status.json` from the `board` branch and shows one card per dark factory lane in its `section#lanes`. Each card shows the lane's task, title, phase, step, how long the task has run and how old its heartbeat is. A lane whose heartbeat is more than 10 minutes old is flagged as stale, and a status that has stopped being published is flagged as not live.

## Context

- Stewart chose this on 2026-09-28 (option 2).
- **The contract:** the ADR from BL-762 fixes the `status.json` schema 1, the status API call (`GET https://api.github.com/repos/StewartScottRogers/Curl/contents/status.json?ref=board` with `Accept: application/vnd.github.raw+json`), the 10-minute staleness rules and the `?status=` and `?now=` overrides.
- **Where the cards go:** BL-757 built the page with a list of data sources and an empty `section#lanes`. Add the status as the second source, which makes the refresh interval 120 s by the page's own rule, and render into that section. Do not restructure the kanban code.
- **Publishing does not need to exist yet.** BL-760 publishes the real file, and until the next shift runs after it lands, the `board` branch does not exist and the call answers 404. Build against fixtures.
- **Local preview:** `dotnet run --file .github/gource/serve.cs -- .github/board 8000`.
- **Headless check:** `"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" --headless=new --disable-gpu --virtual-time-budget=10000 --dump-dom "<url>"` (see BL-757).
- **Tooling:** plain HTML, CSS and JavaScript inline in `index.html`, with no external resource.

## Acceptance criteria

- [x] `.github/board/fixtures/status.json` is schema 1 with `"state": "running"`, `publishedAt` of `2026-09-28T13:12:00Z`, and three lanes:
  - lane 1, running `BL-757` since `12:40:10Z`, heartbeat `13:11:42Z`, phase `run`, step `test` (if `fixtures/tree.json` has no `Tasks/Doing/BL-757-*.md`, add one);
  - lane 2, holding a task, heartbeat `12:50:00Z`;
  - lane 3, with no task (`null`s), phase `wait`, heartbeat `13:11:00Z`.
- [x] Dumping `http://localhost:8000/site/index.html?tree=../fixtures/tree.json&status=../fixtures/status.json&now=2026-09-28T13:14:00Z` with headless Edge gives three lane cards in lane order:
  - Lane 1 shows `BL-757`, its full title from the JSON, `run`, `test`, and a run time of `33 min` or the like. Its task ID links to the task's file path found in the tree.
  - Lane 2 carries a `stale` class and the visible word "stale", because its heartbeat is 24 minutes old.
  - Lane 3 says it holds no task.
  - A lane numbered `0` would be labelled "Runner".
- [x] With `now=2026-09-28T13:40:00Z`, the lanes section shows a banner saying the status has not been updated since 13:12 UTC and the shift may have stopped, and every lane is marked stale.
- [x] With a fixture copy whose `state` is `ended`, the banner says the shift ended at `publishedAt` and no lane is marked stale.
- [x] With a fixture copy whose `schema` is 2, the section shows a notice instead of lane cards.
- [x] With `status=` pointing to a missing file (404), the lanes section shows one quiet line, "No dark factory status published yet", and the kanban still draws.
- [x] The default refresh interval is now 120 s. The page footer says so, and the rate-limit handling from BL-757 counts both calls.
- [x] The kanban checks from BL-757's acceptance criteria still pass against `fixtures/tree.json`.

## Notes

- The status is the second entry in `sources` (`name: 'dark factory status'`), so the
  refresh interval, the footer and the rate-limit check (`remaining < sources.length`)
  all count two calls without further change. `fetchJson` gained optional `headers` and
  `nullStatuses` arguments: the status call sends
  `Accept: application/vnd.github.raw+json` and turns a 404 into `null`, which
  `renderLanes` shows as "No dark factory status published yet". The kanban code is
  unchanged apart from the tree source keeping `latestTree` for lane links.
- Choices (defaults taken): lane cards are sorted by `lane`; durations read `N s`,
  `N min` or `H h M min`; banner times are UTC `HH:MM UTC`; a lane task missing from the
  tree shows its ID unlinked; the run time is shown only while the lane holds a task.
  With `state: ended` no lane is stale, whatever its heartbeat age.
- Lane 2 of the fixture holds `BL-674`, the other `Tasks/Doing` task in `tree.json`, so
  no tree entry was added (`BL-757` was already there).
- Verified with headless Edge against `serve.cs` on 2026-09-28, using temporary fixture
  copies (not committed) for `ended`, `schema: 2` and lane `0`: all six cases rendered
  as the criteria say, the kanban still showed 5 columns of 2, both archive rows
  (`3 tasks`, `2 tasks`), no README card, and the no-external-resource check found
  nothing. `dotnet build` clean; fast tests green (23 assemblies).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The task board page shows one live card per dark factory lane from status.json, flagging stale heartbeats and a stopped shift
