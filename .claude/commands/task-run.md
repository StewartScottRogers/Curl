---
description: Claim the next ready task (or a named one) from the Tasks board, deliver it through its pipeline, and file it as Done or Blocked.
argument-hint: [BL-### | all] — empty takes the next ready task; all keeps going until none is ready
---
Work the task board. Argument: **$ARGUMENTS**

Read `.claude/skills/task-board/SKILL.md` first; its rules are binding. `$TB` below
means
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1`.

## 1. Select
- **A task ID was given:** use it. The script will refuse the claim if the task is
  assigned to Stewart, waiting on dependencies, or in a state that cannot move to
  `Doing`; report its reason and stop.
- **Empty or `all`:** `$TB next`. If it prints `No task is ready.`, run `$TB status`,
  report why nothing is ready (waiting on dependencies, waiting on Stewart, or
  blocked, and on what), and stop.
- **An interactive-only task** (`lane: no`, marked `[ready, interactive only]` by
  `$TB status`) is never picked by `next`, so it must be named by ID:
  `/task-run BL-###`. No dark factory lane is offered one.

## 2. Claim
If the task is already in `Doing` because a dark factory lane claimed it for this run,
skip the move. Otherwise `$TB move -Id <ID> -To Doing` before touching any other file. Then read the task file
at its new path. It is the specification: its `Goal`, `Context` and
`Acceptance criteria` are what "done" means.

### 2a. Narrow a whole-project `touches` (dark factory lanes)
While this task's `touches` names a whole project, no other lane may start a task in that
project. When `touches` names a whole `Curl.Engine.UnitLibrary`, `Curl.Engine.UnitTests`,
`Libcurl` or `Libcurl.UnitTests`, first read the code the task points at. If that shows
which files the change lands in (production files and the test files its tests belong in),
narrow it before you edit any code, so other lanes can start Engine tasks beside this one
(ADR-0694):
1. Change only the task file's `touches:` line, replacing the whole-project entries with
   those files' repo-relative paths. Keep every other entry. Add one `Notes` line saying
   what it was narrowed from and why. Never change the acceptance criteria.
2. With nothing else changed in the worktree (`git status --short` lists only the task
   file), publish it at once: `git add <task file>`, then
   `git commit -q -m "chore(tasks): narrow <ID>'s touches to files" -m "[skip ci]"`, then
   `git push -q origin HEAD:work/dark-factory`. If the push is refused,
   `git pull -q --rebase origin work/dark-factory` and push again, up to three times.
   If it still fails, go on without narrowing.
3. If the code does not show where the change lands, or the change spreads across the
   project, leave `touches` as it is. Too broad is safe; too narrow lets two lanes edit
   one file at once.

Tasks you file (step 4) follow the same rule: name the files in those four projects
when you know them.

## 3. Deliver, by the task's `pipeline`
- `feature`: run the `/feature` stages, with the task file's `Goal`, `Context` and
  `Acceptance criteria` as the feature text, given verbatim to `protocol-architect`.
  The acceptance criteria are gates in addition to each stage's own gate.
- `protocol`: run the `/protocol` stages for the scheme the task names, on the same
  terms.
- `docs`: delegate to `align-and-document` with the task file, then invoke the `verify`
  skill if it touched any `.cs` or project file.
- `direct`: make the change yourself, then invoke the `verify` skill.

As you go, record the plan's summary and anything learned under the task's `Notes`,
and tick each acceptance box as you verify it.

Tick a box only after running its check. A criterion about the build or the tests is
checked by running them, even when only text files changed. Never reword, delete or split
an acceptance criterion during a run: one you cannot meet as written sends the task to
`Blocked` (step 4), and `task-board.ps1` refuses the move to `Done` of a task whose
criteria differ from when it was claimed (BL-2488).

A fix found through an upstream case also gets a focused unit test: any product fix found
through an upstream case (the ratchet, a gap finding or an audit conformance finding) also
adds a focused unit test in the fixed library's own `.UnitTests` project, so each library's
tests stand alone without the upstream cases (ADR-0482).

Up to nine lanes run on this machine at once, each in its own worktree:
- Measure with `Measure-CodeQuality.ps1 -Library <name>[,<name>...]`, naming every
  library the task changed in one call: it runs only the test projects that reach them,
  once. Never measure the whole solution for a task's coverage criterion (BL-1318), and
  never one library at a time: Curl.Console, Curl.Networking.UnitLibrary,
  Curl.Protocol.Abstractions.UnitLibrary and Curl.Original.Protocol.Http.UnitLibrary each reach
  nearly every test project, so four separate runs cost BL-1360 its whole time limit.
  Quote any path you pass (`-ResultsDirectory "C:\Users\Stewart Rogers\..."`): an
  unquoted space split one run's path and left coverage files in the worktree.
- To resume a task from its stash, apply only the stash's changes, never whole files:
  `git diff <sha>^1 <sha> -- <files> | git apply --3way` for tracked files, and
  `git checkout <sha>^3 -- <new files>` for the ones it added (`git stash show` does not
  list those). Copying files from the stash undoes what other lanes changed since.
- Stop only a process you started yourself, by its PID. Never stop processes by name or
  command line across the machine (`Stop-Process -Name dotnet`, or every process whose
  command line holds `Measure-CodeQuality`): those are other lanes' runs, and killing
  them cost five tasks their whole time limit on 2026-10-03.

## 4. File the outcome
A task never stays in `Doing` when you finish. It ends in one of these:
- **Finished.** Every box is ticked and every gate is green:
  `$TB move -Id <ID> -To Done -Reason "<one line: what now works>"`.
- **Cannot finish.** A gate cannot be passed without a decision, an approval, an ADR,
  or work outside this task:
  `$TB move -Id <ID> -To Blocked -Reason "<what blocks it, and who can clear it>"`.
  If the blocker is itself work, have `task-planner` file it first, then add its ID to
  this task's `depends-on`.
- **Follow-up work found** (an option still unimplemented, an edge case set aside):
  have `task-planner` file it as new tasks. Never widen the current task to absorb it.
  Give it file-level `touches` in the Engine and Libcurl projects when this run showed
  where the change lands (step 2a).

## 5. Repeat or report
With `all`, go back to step 1 until nothing is ready or a task ends `Blocked`. Stop at
the first `Blocked` so the user sees it before more work piles on top.

For each task, report its ID, its outcome, what now works, the projects touched, test
counts, and the IDs of any tasks filed. Do not commit unless the user asks.
