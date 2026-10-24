# ADR-0811 — The CI watch reads a running run's failed jobs and cancels a run that lacks a waiting CI fix

- Status: Accepted
- Date: 2026-10-24
- Task: BL-3365, BL-3454, BL-3455, BL-3456, BL-3457 (AF-0388, AF-0407, AF-0408, AF-0409, AF-0410)
- Decided by Claude under Stewart's delegation.
- Amends ADR-0693 for the factory's own cancelling; `ci.yml` is unchanged.

## Context

The process auditor found five red spells on `work/dark-factory` of 37 to 77 minutes, each
against a fix that took about 10 minutes to land. The time went to two things:

1. The coordinator's CI watch read only finished runs. A run's Build and test jobs fail 6 to 8
   minutes into it, but the run ends only when the Quality gates job does, 7 to 18 minutes later
   (run 40034290028: test1173 failed at 22:48:58Z, its job ended at 22:50:02Z, the run at
   22:57:04Z). The failure was filed that much later.
2. Since ADR-0693 a running run on `work/dark-factory` always finishes, and only the waiting run
   is replaced by a newer push. A fix that landed after the red run ended therefore waited behind
   a run of a commit without it, which failed the same way (39906620565, 40011412660,
   40023582861, 40026750438, 40035852439), and green came a whole run later.

The longest job (Quality gates, 13 to 25 minutes) sets a floor under any spell that `ci.yml`, a
guard file, would have to change; that is left to the audit branch.

## Decision

- Each heartbeat the watch also reads the run still running: once one of its jobs has failed, it
  reads that job's log (`gh api repos/{owner}/{repo}/actions/jobs/<id>/logs`, readable as soon as
  the job ends, unlike `gh run view --log-failed`), and adds the run, newest, to the runs the
  verdicts read, with only its failing tests and build errors. The same rules decide what to
  file, and a failure filed early is settled for that run id, so the finished run does not file
  it again.
- While the newest finished run is red, the watch cancels the running run when the run waiting
  behind it is on a commit that brought a CI-fix task (`Fix CI failure`, `Fix CI build`,
  `Fix flaky CI test`) into `Tasks/Done` that the running commit lacks. The waiting run, the
  newest commit with the fix, starts at once. It never cancels while CI is green, so the red
  spell is already on record in a finished run and the cancelled run hides none of it; ADR-0693's
  reason (Linux and macOS untested for hours) does not apply, as the waiting run tests a newer
  commit straight away.

## Consequences

- A spell is now about the fix's time from the failing job plus one run, not the fix's time from
  the run's end plus up to two runs.
- Spells can still pass 30 minutes when the fix takes longer than the red run's Quality gates
  tail, since one full run is 15 to 34 minutes; shortening that is a `ci.yml` change.
- The process tasks wait in Backlog until a shift has run with this, then measure the live logs
  (ADR-0761).
