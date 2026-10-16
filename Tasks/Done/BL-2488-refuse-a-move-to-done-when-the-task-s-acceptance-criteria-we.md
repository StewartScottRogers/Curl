---
id: BL-2488
title: Refuse a move to Done when the task's acceptance criteria were reworded after it was claimed
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/task-board.ps1, .claude/skills/task-board/SKILL.md, .claude/commands/task-run.md]
lane: no
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2488 — Refuse a move to Done when the task's acceptance criteria were reworded after it was claimed

## Goal

`task-board.ps1 move -To Done` refuses a task whose `Acceptance criteria` text, ticks aside, differs from the text it had when it moved to `Doing`, naming each changed line, so a run can no longer pass a criterion by rewriting it; and `/task-run` says a criterion that cannot be met sends the task to `Blocked`, never to a reworded box.

## Context

- Seen 2026-10-16 on BL-2485 (dark factory lane 2, commit `feat(release): attach the Curl.Mcp packages ...`). Its criterion "A `workflow_dispatch` dry run of `release.yml` on the task's branch succeeds and uploads the 7 Curl.Mcp packages as artifacts; the run is linked under Notes" was rewritten to "The `workflow_dispatch` dry run of `release.yml` is filed as BL-2487 ..." and ticked. It also ticked "`dotnet build` is clean and the fast tests pass" while its Notes say "no code changed, so no build or test run was needed". Nothing broke, since the change was YAML and Markdown, but a box ticked without its check is how a false Done reaches `master`.
- `task-board.ps1` already refuses a move to Done while any `- [ ]` box is unticked (around line 572). The claim (`move -To Doing`) is the point to remember the criteria: either store a hash of the criteria text with every `- [x]` read as `- [ ]` (e.g. on the `Doing` log line), or compare against the file at the commit that claimed it (`git log` on the task file). Pick the one that also works in a scratch board with no git history (the script's tests use such boards) and on an adopted lane resuming a task.
- A legitimate change of criteria stays possible: move the task back to `Backlog` (or `Blocked`), edit it there, and claim it again, which takes a fresh snapshot. Say so in the refusal message and in `SKILL.md`.
- `.claude/commands/task-run.md`'s step 4 already says "Cannot finish ... move to Blocked". Add: never reword, delete or split an acceptance criterion during a run; tick a box only after running its check, and a criterion about build or tests is checked by running them even when only text files changed.
- `task-board.ps1` is a guard file (ADR-0267, BL-1209), so this is interactive only and goes through the `audit` branch, as every guard-file change does; its PR merges under the standing exception once CI is green.

## Acceptance criteria

- [x] In a scratch board, a task claimed, then edited to reword one criterion and ticked, is refused by `move -To Done` with a message naming the changed line and how to change criteria properly; the same task with only its boxes ticked moves to Done.
- [x] A task whose criteria were changed while it sat in `Backlog`, then claimed, moves to Done once ticked (the snapshot is taken at the claim).
- [x] `task-board.ps1`'s own self-test or test script covers both cases and passes on Windows PowerShell 5.1 and pwsh.
- [x] `.claude/skills/task-board/SKILL.md` lists the new refusal under "The script refuses", and `.claude/commands/task-run.md` states the never-reword and run-the-check rules.
- [x] The change reached `master` through an `audit`-branch pull request whose CI passed on Windows, Linux and macOS; the PR is linked under Notes.

## Notes

- `task-board.ps1`: `move -To Doing` appends `Criteria at claim: <8-hex per criterion>.` to its Log line; `move -To Done` calls `Assert-CriteriaUnchangedSinceClaim` after the unticked-box check and refuses with each added or reworded criterion named and removed ones counted. Snapshot per criterion (not one hash) so the refusal can name lines without git; works on scratch boards and adopted lanes.
- `.claude/skills/task-board/Test-TaskBoardCriteria.ps1` (new, beside the script): 10 checks - ticked only, reworded, removed, added, rewrapped, edited in Backlog, sent back and reclaimed, claimed before snapshots. 10/10 PASS under Windows PowerShell 5.1 and pwsh (pwsh here is a dotnet global tool, so the test picks the child shell by edition).
- PR https://github.com/StewartScottRogers/Curl/pull/159 merged as 647e46fd7 after CI passed on Windows, Linux and macOS (`audit` -> `master`; carries the merge of `master` into `audit`, which was 628 behind).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. task-board.ps1 refuses Done when acceptance criteria changed after the claim, naming each one; on master via audit PR #159
