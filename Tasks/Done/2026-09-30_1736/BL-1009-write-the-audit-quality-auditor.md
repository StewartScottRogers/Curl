---
id: BL-1009
title: Write the audit-quality auditor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001, BL-1004]
touches: [.claude/agents/audit-quality.md, Audit/Instructions/Quality.md]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1009 — Write the audit-quality auditor

## Goal

A read-only `audit-quality` agent finds tests that do not check what their names claim, weak assertions, and code that survives mutation, and reports them in the audit report format.

## Context

Interactive only (`lane: no`): it writes `.claude/agents/audit-*` and `Audit/`. Run it
with `/task-run BL-1009`. Design: the ADR from BL-994. Rules every auditor follows:
`Audit/Instructions/Auditor-Rules.md` (BL-1001). Report format:
`Audit/Instructions/Report-Format.md` (BL-1001).

Two files:

- `.claude/agents/audit-quality.md` - front matter `name: audit-quality`, a
  `description` saying what it audits and that it reads and reports and never edits,
  `tools: Read, Grep, Glob, Bash` (no Edit, Write or NotebookEdit), `model: sonnet`. The
  body is short: its one job, "read `Audit/Instructions/Auditor-Rules.md`, then
  `Audit/Instructions/Quality.md`, and follow them", and the report requirement.
- `Audit/Instructions/Quality.md` - the method:
  1. Test names against bodies: for a sample of test methods per `.UnitTests` project
     (all of them for projects under 200 tests), does the body exercise the behaviour
     the name states and assert the outcome the name promises? Flag a test whose name
     promises an exit code, bytes or an exception it never asserts.
  2. Weak assertions: `Assert.IsNotNull` or `Assert.IsTrue(x.Length > 0)` as the only
     check of a value the name specifies; assertions on a mock's own setup; tests with
     no assertion; `[Ignore]`d tests; catch-all `try`/`catch` in tests; `Assert.Inconclusive`.
  3. Mutation: run `Audit/Tools/Invoke-MutationTest.ps1` (BL-1004) on the libraries the
     prompt names (default: the three with the most lines changed since the last
     scorecard's audited commit, found with `git diff --stat`), `-MaxMutants 40`. Each
     surviving mutant in non-trivial code is a finding with the mutant as reproduction.
  4. The quality gates are already measured (`coverage-auditor`, CA1502); do not
     re-measure coverage - a covered line whose mutant survives is the finding this
     auditor adds.
  Severity guidance: a surviving mutant that changes an exit code or output bytes is
  High; a test whose name lies is Medium; a weak but not wrong assertion is Low.

## Acceptance criteria

- [x] `.claude/agents/audit-quality.md` exists with `name: audit-quality`, `model: sonnet` and `tools: Read, Grep, Glob, Bash`, and no Edit, Write, MultiEdit or NotebookEdit tool.
- [x] `Audit/Instructions/Quality.md` states the four steps and the severity guidance above and names `Invoke-MutationTest.ps1` with its parameters.
- [x] `claude agents` (run from the repository root) lists `audit-quality`.
- [x] A trial run, `claude -p --agent audit-quality "Audit <a detached worktree of HEAD> for Curl.Protocol.Dict.UnitLibrary only, with no findings to re-audit"`, ends with one report block that parses with `ConvertFrom-Json` and matches `Report-Format.md`; the command and a summary of the report are recorded under Notes, and the worktree shows no changes afterwards.

## Notes

- On the audit branch (worktree Z:/repos/Curl.auditbranch), commit 21c8d62e, pull request https://github.com/StewartScottRogers/Curl/pull/37.
- `claude agents`: in Claude Code 2.1.284 it lists running sessions, not agent definitions (and needs a TTY; --json gives sessions too), so it cannot show audit-quality. The trial run proves the agent is found instead: `claude -p --agent audit-quality` ran it.
- Trial: a detached worktree of the audit branch at 21c8d62e (Z:/repos/Curl.audit/trial-quality, removed after), fingerprint 8cd7d6542a8fe88367f7c699c31022254abe794a58d7c48af759536cd86ad491. From that worktree: claude -p --agent audit-quality --dangerously-skip-permissions "Audit the tree at Z:\repos\Curl.audit\trial-quality (commit ..., fingerprint ...). Scope: Curl.Protocol.Dict.UnitLibrary only ... no findings to re-audit ... seed 1 ... temporary folder <tmp> ...".
- Report: one json block, parses with ConvertFrom-Json, all six top-level fields, auditor quality, the given commit and fingerprint, 0 findings, 0 re-audits, metrics mutationScore.Curl.Protocol.Dict.UnitLibrary 0.7273. It read all 52 Dict tests (names match bodies, no weak assertions) and ran 35 mutants: 23 killed, 1 timed out, 9 survived, 2 stillborn. The worktree showed no changes afterwards, and no mutation worktree was left.
- The 9 survivors were all ConfigureAwait(false) -> true, which cannot change behaviour in a console app; the auditor correctly filed none. Filed BL-1059 so the mutation tool skips them (they lowered the score from 1.0 to 0.73).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The read-only audit-quality auditor audits test names, assertions and mutation survivors and reports in the audit format; in PR #37, awaiting Stewart's merge.
