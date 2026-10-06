---
id: BL-1018
title: Turn audit findings Stewart accepted into Curl tasks
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001, BL-996]
touches: [Audit/Tools/New-TasksFromAcceptedFindings.ps1, Audit/Triage.md]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1018 — Turn audit findings Stewart accepted into Curl tasks

## Goal

After Stewart marks findings `accepted`, one command files a Curl task for each accepted finding that has none, writes the task ID back into the finding, and leaves `proposed` and `rejected` findings alone.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1018`.
Design: the ADR from BL-994 - a triage step Stewart approves turns accepted findings into
Curl tasks. Finding format: `Audit/Findings/README.md` (BL-1001).

`Audit/Triage.md` - the procedure, in order:

1. Findings arrive `proposed` on the `audit` branch, in the audit pull request.
2. Stewart decides each: he sets `status: accepted` or `status: rejected` in the file
   (in the pull request, or by telling an interactive session which to set). Claude
   never sets either on its own judgement.
3. After the pull request merges to `master`, an interactive session on `master` (or on
   the factory branch after it has merged `master`) runs
   `Audit/Tools/New-TasksFromAcceptedFindings.ps1`, commits the new tasks and the
   updated findings, and pushes.
4. The factory works the tasks like any other; the finding stays `accepted` until a
   re-audit closes it (BL-1016).

`Audit/Tools/New-TasksFromAcceptedFindings.ps1`:

- Refuses to run when `CURL_DARK_FACTORY_LANE` is set.
- For each finding with `status: accepted` and `task: none`: runs
  `task-board.ps1 new -Title "<imperative title from the finding>" -Priority <High for
  Critical and High, Normal for Medium, Low for Low> -Pipeline <feature for code, docs
  for truthfulness findings, direct for process findings> -Touches <the project folders
  named in the finding's location>`, then fills the task's Goal, Context (the finding
  ID, its evidence and reproduction, and "closes only when a re-audit confirms the fix")
  and Acceptance criteria (the reproduction no longer reproduces, plus the build and
  fast tests), and writes the new ID into the finding's `task` field.
- The tasks it files are lane-eligible: they change product code, not audit paths. It
  never passes `-Touches` naming an audit path.
- `-WhatIf` prints what it would file and changes nothing.

## Acceptance criteria

- [x] On a scratch board and a scratch findings folder (via `CLAUDE_PROJECT_DIR` pointing at a temporary copy), with one `accepted` finding without a task, one `accepted` with a task, one `proposed` and one `rejected`: exactly one task is filed, with the mapped priority and pipeline, no template comment left in it, and the finding's `task` field names it; the other three findings are byte-identical afterwards.
- [x] `-WhatIf` on the same input changes no file.
- [x] With `CURL_DARK_FACTORY_LANE=1` it exits non-zero with a message and changes nothing.
- [x] `Audit/Triage.md` states the four steps above, including that only Stewart accepts or rejects.
- [x] ASCII only; runs under PowerShell 7 and Windows PowerShell 5.1.

## Notes

- Audit branch commit 131076cd: Audit/Triage.md and Audit/Tools/New-TasksFromAcceptedFindings.ps1.
- -SelfTest (scratch board via CLAUDE_PROJECT_DIR, scratch findings: accepted without a task, accepted with BL-900, proposed, rejected): 8 PASS, 0 FAIL under Windows PowerShell 5.1 and PowerShell 7.6.6 - refused with CURL_DARK_FACTORY_LANE=1 and nothing changed; -WhatIf changed no file; exactly one task filed, priority High and pipeline feature for a High quality finding, touches [Curl.Cli.UnitTests] from the location, no template comment left; the finding's task field names it; the other three findings byte-identical.
- ASCII only (byte scan; no byte outside printable ASCII).
- Decided: touches are the first path segment of each location part, when it is an existing repository folder and not an audit path; none otherwise (the task runs alone). Title 'Fix AF-NNNN: <finding title>'.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. New-TasksFromAcceptedFindings.ps1 files a lane-eligible task per accepted finding and links it; Triage.md sets out the steps; on the audit branch.
