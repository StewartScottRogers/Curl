---
id: BL-994
title: Record the audit office design in an ADR
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-994 — Record the audit office design in an ADR

## Goal

An Accepted ADR under `Documentation/Planning/Decisions/`, marked "Decided by Claude under Stewart's delegation", records the audit office design Stewart agreed on 2026-09-29, so every later audit task cites one source.

## Context

On 2026-09-29 Stewart asked for an in-repository AI audit office: specialist auditors
that periodically audit the dark factory and the code it produced for gaps in quality,
security, performance and process. The design below was agreed with him; this task
records it, it does not reopen it. Tasks BL-995 to BL-1022 build it.

Record, in the ADR's usual sections (see any recent ADR and the `README.md` index):

1. **Independence through four guard layers the factory cannot get past.**
   - `Audit/` is a shared project (like `Tasks/` and `Documentation/`) holding auditor
     instructions, the planted-defect catalogue, findings, scorecards, audit tools and
     the CI guard script. Auditor agents live in `.claude/agents/audit-*.md`. These two
     are the *audit paths*.
   - Tasks that write audit paths are interactive only (`lane: no`, BL-993), and
     `task-board.ps1` refuses, for a dark factory process only, filing a lane-claimable
     task that touches an audit path and claiming an interactive-only one (BL-996).
   - Every dark factory process carries `CURL_DARK_FACTORY_LANE` (BL-995); a PreToolUse
     hook refuses Edit/Write/MultiEdit/NotebookEdit and Bash/PowerShell calls that name
     an audit path when it is set (BL-997).
   - CI fails when `work/dark-factory` changed an audit path since its merge base with
     `master` (BL-998); red CI blocks the shift-end merge, and the coordinator's CI watch
     files an interactive-only task for it (BL-999).
   - Audit sessions work on their own `audit` branch; findings and scorecards reach
     `master` only through a pull request Stewart approves each time. There is no
     standing exception for that merge. Each scorecard records a SHA-256 fingerprint of
     the auditor definitions it ran with (BL-1002).
2. **Six read-only specialist auditors, one job each:** quality (tests that do not check
   what their name claims, weak assertions, hand-built mutation testing), security
   (hand-built crypto, TLS, SSH: fuzzing hostile input, timing leaks, secrets reaching
   logs), performance (speed, memory and startup against real curl on the same transfers;
   the native AOT build), conformance (differential testing of real curl against Curl on
   generated command lines, building on `conformance-auditor` and
   `Record-CurlExchange.ps1`), truthfulness (names, docs and ADRs against the code - it
   reports only; `align-and-document` still owns fixing), process (the factory's logs
   in `<repo>.logs` and git history: time per task, redone work, time with CI red, idle
   lanes, overlap-caused serialisation, token cost). Auditors judge the code before
   reading the factory's ADRs and task logs, so they do not inherit its reasoning.
   Security and conformance run on Opus; the other four and the seeder on Sonnet.
3. **Findings** live in `Audit/Findings/` with severity, evidence and a reproduction. A
   triage step Stewart approves turns accepted findings into Curl tasks. A finding closes
   only when a re-audit confirms the fix, never because a factory task reached Done.
4. **Planted defects.** Before each audit a seeding agent plants 5 to 10 known defects
   from a catalogue on a throwaway detached worktree; each auditor's catch rate is
   reported beside its findings, and an auditor that misses a defect planted for it has
   its findings flagged unreliable in that scorecard.
5. **Scorecard and cadence.** One fixed-format scorecard per audit, so trends show. Audits
   run on demand, before each roadmap-milestone merge to `master`, and after changes to
   `RunDarkFactory.ps1`; they run between shifts, never during one, because they spend
   the same Claude budget. They start with `Audit\RunAudit.cmd -NewTab`, in herdr like
   the factory.
6. **Tooling rules.** PowerShell or C# file-based apps only (no Python), base class
   library only, no package. Audit tools under `Audit/Tools/` are not product code and
   are not held to the coverage gates.

Why each choice, for the ADR's rationale: the factory is the party audited, so anything
it can edit it can (even unintentionally) weaken - hence guards outside its reach and
approval by Stewart rather than by a standing exception; planted defects measure whether
an auditor would have noticed, which a clean report alone cannot show; reading code before
rationale stops an auditor from adopting a wrong justification; closing on re-audit
stops "task Done" from standing in for "defect gone".

## Acceptance criteria

- [x] A new `ADR-NNNN-*.md` (next free number in `Documentation/Planning/Decisions/README.md`'s index) exists with status Accepted, dated 2026-09-29 or later, and the line "Decided by Claude under Stewart's delegation".
- [x] It states each of the six numbered points above, naming the audit paths exactly as `Audit/` and `.claude/agents/audit-*`, the environment variable `CURL_DARK_FACTORY_LANE`, and the `audit` branch.
- [x] It says a finding closes only on a confirming re-audit, and that an auditor missing a defect planted for it is flagged unreliable.
- [x] It names the models: Opus for security and conformance, Sonnet for quality, performance, truthfulness, process and the seeder.
- [x] The `README.md` index lists it.

## Notes

- Recorded as ADR-0267 (next free number; 0266 was the highest in the index). Written directly rather than through align-and-document: the task is one new document whose content the task fixes verbatim, and no `.cs` or project file changed, so `verify` is not needed beyond the build and fast tests run anyway.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0267 records the audit office design and the index lists it
