---
id: BL-1746
title: Guard Gap/ and the gap analyst agents as audit paths so the factory cannot move its own yardstick
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1745]
touches: [.claude/hooks/guard-audit-paths.ps1, .claude/skills/task-board/task-board.ps1, .claude/skills/task-board/SKILL.md, Audit/Guard/Test-AuditPathsUntouched.ps1, Audit/README.md, CLAUDE.md]
lane: no
requirement: none
created: 2026-10-08
completed:
---
# BL-1746 — Guard Gap/ and the gap analyst agents as audit paths so the factory cannot move its own yardstick

## Goal

`Gap/` and `.claude/agents/gap-*` are audit paths in all three guard layers:

- the PreToolUse hook refuses a lane any read or change of them;
- `task-board.ps1` treats a task touching them as interactive only;
- CI's audit guard fails `work/dark-factory` when it changes them.

From then on, the factory cannot read or move the yardstick it is measured by.

## Context

This is ADR-0433 decision 7. The lanes built the office (BL-1719 to BL-1745), and now it is
closed to them. Interactive only (`lane: no`): it changes guard files, which change only
through the `audit` branch (root `CLAUDE.md`, "Audit office"). Run it with
`/task-run BL-1746` in an interactive session. Do the work in the audit branch's worktree,
cut from or merged with `origin/master`, never by rebase. Open the `audit` to `master` pull
request and merge it once CI is green on Windows, Linux and macOS (Stewart's standing
exception, 2026-09-30). Then merge `master` into `work/dark-factory`.

**Where the audit paths are listed today:**

- `.claude/hooks/guard-audit-paths.ps1`: the match at line 43,
  `(^|/)Audit(/|$)` or `(^|/)\.claude/agents/audit-[^/]*$`; the refusal text at line 37;
  the header at line 15.
- `.claude/skills/task-board/task-board.ps1`: `Test-AuditPath` (line 205) and its comment
  (line 197), and the help text at line 19.
- `Audit/Guard/Test-AuditPathsUntouched.ps1`: the guarded patterns (line 55), the header
  (line 24), and its `-SelfTest`. The self-test cross-checks `task-board.ps1`'s
  `Test-AuditPath` (lines 80 to 87) and fails when the two lists disagree.
- The text that names them: `.claude/skills/task-board/SKILL.md` ("An **audit path**
  belongs to..."), `Audit/README.md` ("Independence"), and root `CLAUDE.md` ("Audit
  office": "Audit paths are `Audit/` and `.claude/agents/audit-*`").

**Change.** Add `Gap` (and anything under it, in any letter case) and
`.claude/agents/gap-*` everywhere `Audit` and `.claude/agents/audit-*` appear. Name ADR-0433
beside ADR-0267 in the refusal text and comments. Extend each self-test with a `Gap/` path
and a `gap-*` agent, both positive, and the negative cases `Gaps.md` at the root and
`.claude/agents/gapper.md`, which must not match. Check before committing that nothing on
`work/dark-factory` would break: `git log origin/master..origin/work/dark-factory --name-only`
must show no `Gap/` change that the guard would then reject. If it shows one, merge
`work/dark-factory` to `master` first, through the shift-end route, or wait for it.

`ADR-0267` is immutable and still says "the audit paths are exactly two". ADR-0433 records
the extension, so do not edit ADR-0267.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Audit/Guard/Test-AuditPathsUntouched.ps1 -SelfTest` passes, including the new `Gap/` and `gap-*` cases and the cross-check against `task-board.ps1`.
- [ ] With `CURL_DARK_FACTORY_LANE=1` set, `task-board.ps1 new -Title x -Touches Gap/Tools/x.ps1` is refused without `-NoLane`, and `status` shows an existing task touching `Gap/README.md` as interactive only.
- [ ] The hook refuses a lane's `Read` of `Gap/README.md` and `.claude/agents/gap-options.md`, and allows `Gaps.md` and `.claude/agents/gapper.md`. Show this by running the hook script with a sample tool-call JSON on standard input, as its existing tests do.
- [ ] `SKILL.md`, `Audit/README.md` and `CLAUDE.md` name `Gap/` and `.claude/agents/gap-*` as audit paths.
- [ ] The change reached `master` through a green `audit` pull request, and `work/dark-factory` has merged `master`. Record the pull request's number in this task's Notes.

## Notes

## Log

- 2026-10-08: Created.
