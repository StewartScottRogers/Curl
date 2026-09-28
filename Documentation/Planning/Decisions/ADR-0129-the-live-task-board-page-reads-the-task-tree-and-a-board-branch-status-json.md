# ADR-0129 — The live task board page reads the task tree and a `board` branch `status.json`

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-762.
Stewart chose the overall design on 2026-09-28: option 1, a static page that reads the
GitHub REST API in the viewer's browser and draws the board as a kanban, and option 2,
dark factory lane heartbeats published to a force-pushed `board` branch and shown on
that page as one live card per lane. This ADR fixes the details both sides build
against.

## Context

Stewart wants to watch the task board and the dark factory's lanes from a browser
without cloning the repository. Two pieces are being built at once by different tasks:
the dark factory writes lane status (BL-759, BL-760) and the page reads it (BL-757,
BL-761). They need one written contract before either is built.

Measured on 2026-09-28, unauthenticated:

- `GET https://api.github.com/repos/StewartScottRogers/Curl/git/trees/work/dark-factory?recursive=1`
  returns 200. Writing the slash in the branch name as `%2F` returns the same.
- The response carries `Access-Control-Allow-Origin: *`,
  `Cache-Control: public, max-age=60` and `X-RateLimit-Limit: 60`, and exposes
  `X-RateLimit-Remaining` and `X-RateLimit-Reset` to scripts.
- It is about 1.4 MB, with 777 `Tasks/` entries and `"truncated": false`.
- `GET .../contents/status.json?ref=board` returns 404, because the `board` branch
  does not exist yet.

Other facts that shape the decision:

- Task file names come from `.claude/skills/task-board/task-board.ps1`: the title is
  lowercased, every run of characters outside `[a-z0-9]` becomes `-`, hyphens are
  trimmed, the result is cut at 60 characters and a trailing hyphen is trimmed again.
  A file name gives the ID exactly, but the title only lossily: case, punctuation and
  anything past 60 characters are lost.
- The GitHub Pages site is served from the `gource` branch
  (https://stewartscottrogers.github.io/Curl/), built by `.github/workflows/gource.yml`.
- `RunDarkFactory.ps1` already keeps per-lane state files in `<repo>.logs\lanes-<stamp>\`
  (`Get-LaneStatePath`, `Set-LaneState`). Its coordinator loop ticks every 5 seconds.
  Lanes are denied `git push` and `git fetch` (`$LaneForbidden`).

## Decision

### 1. Titles come from file names; there is no generated index

A kanban card shows the ID and the slug turned back into words: hyphens become spaces
and the first letter is upper-cased. A `…` is appended when the slug is 59 or 60
characters long, because that length means it may have been cut. The card links to the
file on GitHub, where the full title is. Lane cards show the full title, which
`status.json` carries.

### 2. Kanban source

The page makes one recursive tree call per refresh on `work/dark-factory`. It keeps the
entries matching `Tasks/<State>/BL-*.md` for `Backlog`, `Doing`, `Blocked`, `Deferred`
and `Done`. It collapses `Tasks/Done/<yyyy-MM-dd_HHmm>/BL-*.md` into one row per archive
folder, which shows how many tasks the folder holds and can be expanded. It ignores every
other path. When the response says `"truncated": true`, the page shows a visible
warning. Each card links to
`https://github.com/StewartScottRogers/Curl/blob/work/dark-factory/<path>`.

### 3. Lane source

The page makes one call per refresh:
`GET https://api.github.com/repos/StewartScottRogers/Curl/contents/status.json?ref=board`
with `Accept: application/vnd.github.raw+json`. A 404 means no status has been published,
which is not an error: the page shows no lane cards and says so.

### 4. Refresh budget

The default and minimum refresh interval is 60 seconds times the number of API calls
made per refresh: 60 s with only the tree call, 120 s once lanes are shown. That keeps
one open tab within the unauthenticated limit of 60 calls an hour. When
`X-RateLimit-Remaining` is lower than the calls the next refresh needs, or a call answers
403 or 429, the page says so, shows the reset time, and waits until `X-RateLimit-Reset`.

### 5. Query overrides for testing

- `?repo=owner/name` and `?branch=` choose the repository and the task branch.
- `?refresh=` sets the interval in seconds; a value under the minimum is clamped up to it.
- `?tree=<url>` and `?status=<url>` replace the two API calls, so the page can be
  exercised against fixtures served by `.github/gource/serve.cs`.
- `?now=<ISO 8601>` pins the clock used for staleness.

### 6. Where the page lives

The source is one self-contained file, `.github/board/site/index.html`: plain HTML, CSS
and JavaScript, with no external resource, no framework and no build step. Test fixtures
go in `.github/board/fixtures/`. The page is published onto the `gource` branch as
`board/index.html` and served at https://stewartscottrogers.github.io/Curl/board/.

### 7. The `board` branch

It holds one parentless commit whose tree has exactly one file, `status.json`. Only the
coordinator force-pushes it; a single-runner shift is covered in item 8. The commit is
built with plumbing (`git hash-object -w`, `git mktree`, `git commit-tree`, then
`git push --force origin <commit>:refs/heads/board`), so the coordinator's working tree
and index are never touched. A failed push is traced and never stops a shift.

Lanes never push heartbeats. Each lane writes only its own local file and the
coordinator merges them, so the branch has one writer and lanes cannot collide. Pushes
to `board` start neither the CI nor the Gource workflow (BL-756).

### 8. Heartbeat files and intervals

- Each lane writes `<repo>.logs\lanes-<stamp>\lane-<n>.heartbeat.json`, holding one lane
  object as in item 9. It writes a temporary file and then renames it, so a reader never
  sees half a file.
- A single-runner shift (`-Lanes 1`) is lane `0` and publishes its own status.
- A lane rewrites its file on every phase change and at least every 60 seconds during a
  run.
- The coordinator publishes every `-HeartbeatMinutes`, a new `RunDarkFactory.ps1`
  parameter (default 3; 0 turns publishing off). It publishes once more at shift end
  with `"state": "ended"`.
- The page flags a lane as stale when its `heartbeatAt` is more than 10 minutes old. It
  flags the whole status as not live when `state` is `running` and `publishedAt` is more
  than 10 minutes old.

### 9. `status.json` schema 1

All times are UTC, written `yyyy-MM-ddTHH:mm:ssZ`.

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

| Field | Type | Meaning |
| --- | --- | --- |
| `schema` | number | Always `1` for this format. A reader seeing any other value shows a notice instead of lane cards. |
| `shift` | string | `RunDarkFactory.ps1`'s `$Stamp` for the shift. |
| `branch` | string | The task branch the shift integrates into, e.g. `work/dark-factory`. |
| `state` | string | `running` or `ended`. |
| `publishedAt` | string, UTC time | When the coordinator built this file. |
| `lanes` | array | One lane object per lane, ordered by `lane`. |
| `lanes[].lane` | number | The lane number; `0` for a single-runner shift. |
| `lanes[].task` | string or `null` | The task ID the lane holds; `null` while it holds none. |
| `lanes[].title` | string or `null` | The task's full title from its front matter; `null` while the lane holds no task. |
| `lanes[].phase` | string | One of `starting`, `claim`, `run`, `integrate`, `wait` (nothing can start yet), `tokens` (waiting for the usage limit) and `finished`. |
| `lanes[].step` | string | At most 80 characters: the last tool label from `Get-ToolLabel` (for example `build`, `test`, `edit Foo.cs`), or empty. |
| `lanes[].taskStartedAt` | string, UTC time, or `null` | When the lane started its current task; `null` while it holds none. |
| `lanes[].heartbeatAt` | string, UTC time | When the lane last wrote its heartbeat file. |

The page ignores fields it does not know, so fields can be added without a new schema
number; renaming or removing one, or changing a meaning, takes schema 2.

## Consequences

- BL-756 keeps pushes to `board` from starting the CI and Gource workflows.
- BL-757 draws the kanban from the tree call (items 1, 2, 4, 5, 6).
- BL-758 publishes `.github/board/site/index.html` onto the `gource` branch as
  `board/index.html` and links it.
- BL-759 writes each lane's heartbeat file (item 8, lane object of item 9).
- BL-760 merges the heartbeat files into `status.json` and force-pushes the `board`
  branch (items 7, 8, 9).
- BL-761 draws one live card per lane from `status.json` (items 3, 4, 8, 9).
- Easy: no workflow, token or server is needed; the page is one file that works from any
  browser, and fixtures exercise it offline.
- Hard: titles on kanban cards are lossy; an unauthenticated viewer gets at most 60
  calls an hour, so refreshes are slow; a board past GitHub's tree size limit shows only
  the truncation warning; each heartbeat publish is a force push, so the `board` branch
  has no history.

## Alternatives considered

- **An index file of titles committed to `work/dark-factory`.** Every lane would rewrite
  it, so lane rebases would conflict on it.
- **An index of titles built by a workflow.** It adds a commit for every push.
- **One contents call per task to read titles.** 777 calls against a limit of 60 an hour.
- **Each lane force-pushing its own heartbeat.** The `board` branch is one parentless
  commit, so each lane's push would overwrite the others' status; lanes are also denied
  `git push`. The coordinator, as the single writer, merges the lane files instead.
- **A heartbeat commit on `work/dark-factory`.** It would add history noise and start CI
  on every beat.
