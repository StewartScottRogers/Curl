# ADR-0482: A fix found through an upstream case also gets a focused unit test in its own library

- Status: Accepted
- Date: 2026-10-13
- Decided by Claude under Stewart's delegation (BL-2156)

## Context

Stewart chose three fixes on 2026-10-13 instead of converting the vendored upstream
cases to hand-written C# tests. This is the third. The upstream cases (the ratchet, gap
findings, audit conformance findings) run through the whole of Curl, so a fix they
motivate can be proved only by the case, which needs the vendored upstream data. Lanes
mostly added a unit test beside the fix already; nothing wrote it down.

## Decision

Any product fix found through an upstream case also adds a focused unit test in the
fixed library's own `.UnitTests` project, so each library's tests stand alone without
the upstream cases. The rule is stated in the same words in `CLAUDE.md` (solution-wide
conventions) and in `.claude/commands/task-run.md` (step 3, where a lane finishes a fix).

## Consequences

- A fix reached through a `Close GF-####` or `Fix AF-####` task is not done on the upstream
  case passing alone; the task's acceptance criteria and review check for the unit test.
- The tasks' generated criteria are written by files under `Gap/` and `Audit/`, which a
  lane may not change (ADR-0267, ADR-0433); the rule reaches those tasks through the task-run
  command every lane reads. An interactive session may copy it into those generators.
