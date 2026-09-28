---
id: BL-755
title: Decide the live task board page's data sources and the board branch's status.json format
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-755 — Decide the live task board page's data sources and the board branch's status.json format

## Goal

An ADR records how the live task board page (BL-757, BL-761) gets its data, and the exact `status.json` contract that the dark factory writes (BL-759, BL-760) and the page reads. Both sides then build against one written format.

## Context

- Stewart chose options 1 and 2 on 2026-09-28. Option 1 is a static page on the project's GitHub Pages site that calls the GitHub REST API in the viewer's browser and draws the board as a kanban. Option 2 is dark factory lane heartbeats, published to a force-pushed `board` branch. The page shows the heartbeats as one live card per lane.
- Measured 2026-09-28 by the planner, unauthenticated: `GET https://api.github.com/repos/StewartScottRogers/Curl/git/trees/work/dark-factory?recursive=1` returns 200. It returns the same with the slash written as `%2F`. The response carries `Access-Control-Allow-Origin: *`, `Cache-Control: public, max-age=60`, `X-RateLimit-Limit: 60` and exposes `X-RateLimit-Remaining`/`X-RateLimit-Reset` to scripts. It is about 1.4 MB with 777 `Tasks/` entries and `"truncated": false`. `GET .../contents/status.json?ref=board` returns 404 today because the branch does not exist yet.
- Task file names come from `.claude/skills/task-board/task-board.ps1` (lines 362-363). The script lowercases the title, turns every run of characters outside `[a-z0-9]` into `-`, trims hyphens, and cuts the result at 60 characters, then trims a trailing hyphen. So a name gives the ID exactly, but the title only lossily: case, punctuation and anything past 60 characters are gone.
- The Pages site is served from the `gource` branch (https://stewartscottrogers.github.io/Curl/), which `.github/workflows/gource.yml` builds.
- `RunDarkFactory.ps1` already keeps per-lane state files in `<repo>.logs\lanes-<stamp>\` (`Get-LaneStatePath`, `Set-LaneState`). Its coordinator loop ticks every 5 seconds, and lanes are denied `git push` and `git fetch` (`$LaneForbidden`).
- The decisions below were reached while planning. The task records them, and it may improve a detail only if it says why in the ADR. The ADR lists them under Decision:
  1. **Titles come from file names; there is no generated index.** A card shows the ID and the slug turned back into words: hyphens become spaces and the first letter is upper-cased. A `…` is appended when the slug is 59 or 60 characters long, because that length means it may have been cut. The card links to the file on GitHub, where the full title is. Lane cards show the full title, which `status.json` carries. Rejected alternatives:
     - An index file committed to `work/dark-factory`: every lane would rewrite it, so rebases would conflict.
     - An index built by a workflow: that adds a commit for every push.
     - One contents call per task: 777 calls against a limit of 60 an hour.
  2. **Kanban source.** The page makes one recursive tree call per refresh on `work/dark-factory`. It keeps the entries matching `Tasks/<State>/BL-*.md` for Backlog, Doing, Blocked, Deferred and Done. It collapses `Tasks/Done/<yyyy-MM-dd_HHmm>/BL-*.md` into one row per archive folder that shows how many tasks it holds and can be expanded. It ignores every other path. When the response says `"truncated": true`, it shows a visible warning. Each card links to `https://github.com/StewartScottRogers/Curl/blob/work/dark-factory/<path>`.
  3. **Lane source.** The page makes one call per refresh: `GET https://api.github.com/repos/StewartScottRogers/Curl/contents/status.json?ref=board` with `Accept: application/vnd.github.raw+json`. A 404 means no status has been published, which is not an error.
  4. **Refresh budget.** The default and minimum refresh interval is 60 seconds times the number of API calls made per refresh. That is 60 s with only the tree call and 120 s once lanes are shown, which keeps one open tab within the unauthenticated 60 calls an hour. When `X-RateLimit-Remaining` is lower than the calls the next refresh needs, or a call answers 403 or 429, the page says so, shows the reset time, and waits until `X-RateLimit-Reset`.
  5. **Query overrides for testing:**
     - `?repo=owner/name` and `?branch=` choose the repository and the task branch.
     - `?refresh=` sets the interval in seconds; values under the minimum are clamped up to it.
     - `?tree=<url>` and `?status=<url>` replace the two API calls, so the page can be exercised against fixtures served by `.github/gource/serve.cs`.
     - `?now=<ISO 8601>` pins the clock used for staleness.
  6. **Where the page lives.** The source is one self-contained file, `.github/board/site/index.html`: plain HTML, CSS and JavaScript, no external resource, no framework, no build step. Test fixtures go in `.github/board/fixtures/`. The page is published onto the `gource` branch as `board/index.html` and served at https://stewartscottrogers.github.io/Curl/board/.
  7. **`board` branch.** It holds one parentless commit whose tree has exactly one file, `status.json`. Only the coordinator force-pushes it; single-runner shifts are covered in item 8. The commit is built with plumbing (`git hash-object -w`, `git mktree`, `git commit-tree`, then `git push --force origin <commit>:refs/heads/board`), so the coordinator's working tree and index are never touched. A failed push is traced and never stops a shift. Lanes never push heartbeats. Each lane writes only its own local file, and the coordinator merges them, so there is one writer to the branch and lanes cannot collide. Pushes to `board` start neither the CI nor the Gource workflow (BL-756).
  8. **Heartbeat files and intervals.**
     - Each lane writes `<repo>.logs\lanes-<stamp>\lane-<n>.heartbeat.json`, one lane object as below. It writes a temporary file and then renames it, so a reader never sees half a file.
     - A single-runner shift (`-Lanes 1`) is lane `0` and publishes its own status.
     - A lane rewrites its file on every phase change and at least every 60 seconds during a run.
     - The coordinator publishes every `-HeartbeatMinutes`, a new parameter (default 3; 0 turns publishing off). It publishes once more at shift end with `"state": "ended"`.
     - The page flags a lane as stale when its `heartbeatAt` is more than 10 minutes old. It flags the whole status as not live when `"state"` is `"running"` and `publishedAt` is more than 10 minutes old.
  9. **`status.json` schema 1.** All times are UTC, `yyyy-MM-ddTHH:mm:ssZ`:
     ```json
     {
       "schema": 1,
       "shift": "20260928-130501",
       "branch": "work/dark-factory",
       "state": "running",
       "publishedAt": "2026-09-28T13:12:00Z",
       "lanes": [
         {
           "lane": 1,
           "task": "BL-553",
           "title": "Open an IMAP session ...",
           "phase": "run",
           "step": "test",
           "taskStartedAt": "2026-09-28T12:40:10Z",
           "heartbeatAt": "2026-09-28T13:11:42Z"
         }
       ]
     }
     ```
     - `shift` is `RunDarkFactory.ps1`'s `$Stamp`.
     - `state` is `running` or `ended`.
     - `phase` is one of `starting`, `claim`, `run`, `integrate`, `wait` (nothing can start yet), `tokens` (waiting for the usage limit) and `finished`.
     - `step` is at most 80 characters: the last tool label from `Get-ToolLabel` (for example `build`, `test`, `edit Foo.cs`), or empty.
     - `task`, `title` and `taskStartedAt` are `null` while the lane holds no task.
     - The page ignores fields it does not know. A reader seeing a `schema` other than 1 shows a notice instead of lane cards.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists; the number was checked as unused at the time of writing, because lanes add ADRs concurrently. Its Status is Accepted and it is marked "Decided by Claude under Stewart's delegation", noting that Stewart chose the overall design (options 1 and 2) on 2026-09-28.
- [ ] Its Context holds the 2026-09-28 API measurements above.
- [ ] Its Decision states items 1 to 9, with the `status.json` schema reproduced exactly as a JSON example and a field table.
- [ ] It lists the rejected alternatives for titles and for heartbeat pushing (each lane force-pushing its own file would overwrite the others' single-commit branch).
- [ ] Its Consequences name BL-756, BL-757, BL-758, BL-759, BL-760 and BL-761.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
