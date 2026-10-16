---
id: BL-2521
title: Make task-board dedupe catch an ID shared with an archived Done task
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1, .claude/skills/task-board/Test-TaskBoardDedupe.ps1, .claude/skills/task-board/SKILL.md]
lane: no
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2521 — Make task-board dedupe catch an ID shared with an archived Done task

## Goal

A task filed with an ID that another session's task, already Done and archived, also carries is renumbered by `task-board.ps1 dedupe`, so no two task files share an ID.

## Context

On 2026-10-16 two IDs were given twice:
- BL-2482: `Tasks/Backlog/BL-2482-engine-send-curlopt-httppost-bodies-...` (filed in 1e7386408) and `Tasks/Done/2026-10-16_0832/BL-2482-ftp-read-glibc-s-a-equivalence-classes-...`.
- BL-2496: `Tasks/Doing/BL-2496-accept-proto-lists-naming-an-unknown-scheme-...` (filed in a69fadfd4) and `Tasks/Done/2026-10-16_1113/BL-2496-curl-api-curlclient-lifecycle-...` (filed in ad0b51b55).

Older pairs in the archive (BL-1068, BL-1559) show it has happened before.

Cause: the `dedupe` command in `.claude/skills/task-board/task-board.ps1` groups only `-not $_.Archived` tasks. When one session archives its Done copy of an ID while another session files a new task with that same ID, the pair is never compared. So the live task keeps the duplicate ID, and `status` warns about it on every run.

Rule to keep: a task already on the shared ref keeps its ID; the one that is not yet there is renumbered. An archived task never changes its ID, because history and commit messages name it. When the two collide, the live task is renumbered, whichever of them is on the shared ref.

Guard file: `task-board.ps1` changes only through the `audit` branch (CLAUDE.md, "Audit office"), so this task is interactive only.

## Acceptance criteria

- [x] `task-board.ps1 dedupe -Since <ref>` renumbers a live (not archived) task whose ID an archived Done task also carries, and leaves the archived task's file name and `id:` unchanged.
- [x] Two archived tasks that share an ID are left as they are (no rewrite of history), and `status` no longer warns about archived-only pairs.
- [x] A new `.claude/skills/task-board/Test-TaskBoardDedupe.ps1` builds a scratch board, as `Test-TaskBoardCriteria.ps1` does, and passes for a live and archived pair, an archived-only pair, and two new live tasks, under both Windows PowerShell 5.1 and pwsh.
- [x] `Test-TaskBoardCriteria.ps1` still passes under both Windows PowerShell 5.1 and pwsh.
- [x] The live BL-2482 and BL-2496 on the board are renumbered by running the fixed `dedupe`, and every reference to them in other live task files (for example `depends-on`) follows.
- [x] `.claude/skills/task-board/SKILL.md` describes what `dedupe` now compares.
- [x] The change reaches `master` through a pull request from the `audit` branch with CI green on Windows, Linux and macOS, and `master` is merged into `work/dark-factory`.

## Notesn- Fix in PR #160 (audit -> master, merged with CI green on Windows, Linux and macOS), merged into work/dark-factory as 5dcd86270. Also fixed: the changed-file rewrite compared forward-slash git paths with backslash task paths, so when two new tasks collided it rewrote the kept task's own id line.n- The fixed dedupe renumbered live BL-2496 -> BL-2523 (Deferred) and BL-2482 -> BL-2524 (Done); no other live task named either ID. status now warns about no duplicates.n
## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: b94827b4 7bc07c93 49428525 0d10730e 8f977375 67fc12e5 fbb6e071.
- 2026-10-16: Doing -> Done. dedupe renumbers a live task sharing an archived task's ID; status warns only about pairs with a live task
