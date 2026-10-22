# ADR-0761 — A process finding's task measures the live logs after its fix

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3064, BL-3066, BL-3080, BL-3082, BL-3083, BL-3084
- Decided by Claude under Stewart's delegation.

## Context

`Audit/Tools/New-TasksFromAcceptedFindings.ps1` gave every Fix and Re-fix task the criterion
"the finding's reproduction, run from the repository root, gives the expected result". A process
finding's reproduction runs `Audit/Tools/Measure-FactoryProcess.ps1` over a frozen log copy under
`<repo>.audit\<stamp>\logs`. That copy is history, so the reproduction gives the recorded result
forever, whatever the fix. Six tasks (BL-3064, BL-3066, BL-3080, BL-3082, BL-3083, BL-3084) could
not be finished: lanes did the fix work, then sent each one back or to Blocked as interactive
only, which is itself the redone work the process auditor counts. The process auditor's re-audit
already measures the new log window, and `:cost-outlier` findings already have their own rule
(ADR-0422, amended 2026-10-14).

## Decision

For a process finding about past factory behaviour (any process finding whose key does not end
in `:cost-outlier`), the generator writes a task whose first criterion is the finding's
measurement run over the live logs written after the fix landed: `-LogRoot` the repository's
`.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory`
(the same command `Audit/RunAudit.ps1` saves as `ci-runs.json`), counting only trace lines and
CI runs from the fix's landing time. The task's Notes name the fix, its landing time, the window
and the numbers. With no shift run since the fix, the task waits in Backlog.

The measurement runs a script under `Audit/`, which a lane may not read, so such a task is filed
with `-NoLane` (interactive only). `:cost-outlier` tasks keep their criteria and stay
lane-eligible.

The finding itself still closes only by its auditor's re-audit (ADR-0422); this changes only what
the task must show.

## Consequences

- A process task can reach Done once the fix has run in at least one shift and the live window
  shows the expected result, instead of staying open for ever.
- Process tasks no longer bounce between lanes and Backlog; an interactive session works them.
- The window can be short, so its length is written down, and the re-audit of the next window
  stays the evidence that closes the finding.
