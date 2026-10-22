# ADR-0744 — A lane claims CI-fix tasks past the usage stop

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2946 (AF-0308)
- Decided by Claude under Stewart's delegation.

## Context

AF-0308: CI went red at 20:29 Arizona on 2026-10-19 and BL-2873 and BL-2874, the fix tasks, were
filed two minutes later. Shift 192417 held its lanes at "weekly tokens 97% used" and claimed
nothing; the fixes were claimed 42 minutes after filing, when a later shift started.

## Decision

When `Get-UsageStop` says stop (the weekly window at `-StopAtWeeklyUsage` or the 5-hour one at
`-StopAtUsage`), a lane no longer ends at once if a `Fix CI failure ...` or `Fix flaky CI test ...`
task waits in Backlog. It claims with `Invoke-Claim -CiFixOnly`, which takes the board's next task
only when its title is a CI fix (`Test-CiFixTitle`) and otherwise ends the lane with the same
"tokens low" reason. Any other task still waits for the reset. A run that hits the real limit is
already waited out, so the headroom above the stop percentages pays for the fix.

## Consequences

The red spell is bounded by the lane's claim time, not the window's reset. The audit's reproduction
reads a past run's log, which cannot change; the next audit reads new spells.
