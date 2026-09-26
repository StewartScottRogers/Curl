---
name: task-board
description: Rules and tooling for Curl's task board — the Tasks shared project, where each task is one Markdown file and its folder is its status (Backlog, Doing, Blocked, Deferred, Done, with timestamped archive folders under Done). Use whenever creating, claiming, moving, blocking, deferring, completing or archiving a task, when asked what to work on next, or before editing anything under Tasks/.
---
# Task board

## The model

- One task is one file: `Tasks/<State>/BL-###-<slug>.md`.
- **The folder is the status.** There is no status field; never add one.
- IDs run `BL-000`, `BL-001`, … in one sequence across the whole board, archives
  included. Never reused, never renumbered. Allocate them with the script (`new` or
  `next-id`); never count by hand.
- The `README.md` in each folder is not a task. The script only reads `BL-*.md`.

## Front matter

| Field | Values | Meaning |
| --- | --- | --- |
| `id` | `BL-###` | Matches the file name. |
| `title` | text | Imperative, one line. |
| `priority` | `High`, `Normal`, `Low` | `/task-run` takes ready tasks by priority, then lowest ID. |
| `assignee` | `Claude`, `Stewart` | `Stewart` means a decision or action only he can take. Claude never claims one. |
| `pipeline` | `feature`, `protocol`, `docs`, `direct` | How `/task-run` delivers it (below). |
| `depends-on` | `[BL-###, …]` | Tasks that must be in `Done` or its archive before this one can start. |
| `requirement` | requirement ID or `none` | From `Documentation/Product/Requirements.md`. |
| `created` | `yyyy-MM-dd` | Set by the script. |
| `completed` | `yyyy-MM-dd` | Set by the script when the task moves to `Done`. |

| Pipeline | Delivered by |
| --- | --- |
| `feature` | The `/feature` stages: plan, tests first, implement, verify, review, conformance, document. |
| `protocol` | The `/protocol` stages for the scheme the task names. |
| `docs` | `docs-writer` alone. No C# changes. |
| `direct` | A small mechanical change made in the session (configuration, scripts, solution file), then the `verify` skill. |

## Body

Sections in this order: `Goal`, `Context`, `Acceptance criteria`, `Notes`, `Log`.
`Log` is always last and append-only: one line per event, absolute dates. The script
appends to it, so hand-written entries go there too, never above it.

## What makes a good task

- **Sized for one `/task-run`.** One pipeline run; for code, ideally one library and
  its `.UnitTests` twin. If the goal needs "and then", split it.
- **Ready means a stranger could finish it** without asking a question.
- **Acceptance criteria are checkable from the repository**: a named test that passes,
  a command and its expected result, the `CurlExitCode` a failure returns, output
  bytes that match upstream curl for a named case, a document section that states X.
  "Works correctly" is not a criterion.
- **Dependencies are explicit** and never circular.
- **Stewart's decisions are their own tasks.** A new package, a deliberate divergence
  from upstream curl, the licence, anything the root `CLAUDE.md` says needs his
  approval: file it assigned to `Stewart`, and make the work that waits on it depend
  on it.

## States

| State | Means | Moves to |
| --- | --- | --- |
| `Backlog` | Defined and waiting. | `Doing`, `Blocked`, `Deferred` |
| `Doing` | Claimed by an active run. | `Done`, `Blocked`, `Deferred`, `Backlog` |
| `Blocked` | Wants to proceed and cannot. | `Backlog`, `Doing`, `Deferred` |
| `Deferred` | Chosen not to do now. | `Backlog` |
| `Done` | Finished. | Archive only. Reopening finished work is a new task. |

**Blocked or Deferred?** Blocked means you would continue if one thing changed, so
name that thing and who can change it. Deferred means nobody wants it now, so say why
and when to look again.

## The script — the only way tasks move

```
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 <command> [options]
```

| Command | Options | Does |
| --- | --- | --- |
| `status` | | Every state, with each Backlog task marked ready (and its queue position), waiting on named tasks, or needing Stewart. |
| `next` | | The task `/task-run` takes next, or `No task is ready.` |
| `next-id` | | The next free ID. |
| `new` | `-Title` (required), `-Priority`, `-Assignee`, `-Pipeline`, `-DependsOn BL-001,BL-002`, `-Requirement` | Creates the task in `Backlog` from `TASK-TEMPLATE.md` and prints its path. Fill in the body with an edit afterwards. |
| `move` | `-Id`, `-To`, `-Reason` | Validates the transition, appends the `Log` line, and moves the file. `-Reason` is required for every destination except `Doing`. |
| `archive` | `-OlderThanDays` (default 7; 0 for all) | Moves finished tasks into a new `Done/<yyyy-MM-dd_HHmm>/` folder. |

The script refuses:

- moves the table above does not allow
- claiming a task assigned to Stewart, or one whose dependencies are not done
- moving to `Done` while any `- [ ]` box is unticked
- touching an archived task

Do not work around a refusal. It is telling you something about the task.

Never move a task with `Move-Item`, `git mv` or an editor. The script is what keeps
the log and dates honest.

## Claiming and finishing

1. Claim with `move -To Doing` before the first edit of any other file. If the move
   fails because the task is no longer in `Backlog`, someone else has it; take
   another.
2. One task in `Doing` per session.
3. Tick each acceptance box in the file as you verify it. Move to `Done` only when
   every box is ticked and every pipeline gate is green, with a one-line `-Reason`
   saying what now works.
4. If you cannot finish, move to `Blocked` with the blocker and who can clear it. If
   the blocker is itself work, have `task-planner` file it and add it to this task's
   `depends-on`.
5. Follow-up work you discover becomes new tasks. Never widen the task you are on.
6. A task never stays in `Doing` after the run ends.

## Never

- Edit anything inside `Tasks/Done/<timestamp>/`.
- Delete a task file. Unwanted work goes to `Deferred` with its reason.
- Reuse or renumber an ID, or add a status field.
- Claim a task assigned to Stewart.
- Track work anywhere else. `Documentation/Planning/Backlog.md` is retired.
