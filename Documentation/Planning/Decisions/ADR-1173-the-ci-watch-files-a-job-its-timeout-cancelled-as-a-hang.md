# ADR-1173: The CI watch files a job its timeout cancelled as a hang

- Status: Accepted
- Date: 2026-10-31
- Task: BL-4420
- Decided by Claude under Stewart's delegation.

## Context

GitHub reports a job that ran into its `timeout-minutes` as `cancelled`, and so the whole run. The CI watch
skipped every cancelled run (BL-987), so run 41217938101, whose quality shard hung in `Curl.Console.UnitTests`
for 39 minutes until its 45-minute timeout, filed nothing and the shift-end merge gave up on a cancelled CI.
A run cancelled by a newer push, by a person, or replaced before any job started proves nothing and must stay
ignored. The timeout itself lives in `.github/workflows/ci.yml`, which the factory may not read.

## Decision

`Get-CiRuns` examines a cancelled run through `gh run view --json jobs`. A cancelled job is a hang when it
started (a step ran), ran at least 15 minutes (`$CiHangSilenceMinutes`), and its log (`gh run view --job <id>
--log`) either says it exceeded its maximum execution time or ends with `##[error]The operation was canceled.`
after at least 15 minutes without a line. Each hang becomes a failure of kind `hang`, keyed
`hang in <job> (<assembly>)` from the last `Test run for` assembly, filed at once as a High task
`Fix CI hang in <job> (<assembly>)` whose Context names the run, commit, job, assembly and the time of the last
line before the silence. A run with no hang is skipped as before.

The silence is the test chosen because it needs no read of `ci.yml` and only one log read per long cancelled
job: a push or a person cancels a job mid-output, so its last line comes moments before the cancel, while a
hang leaves the log silent until the timeout. The job's duration filters out short cancelled jobs before any
log is read.

## Consequences

- A hang is filed instead of leaving the shift-end merge blocked on a cancelled run with nothing on the board.
- A job a person cancels after it has been silent 15 minutes is read as a hang; that job was stuck anyway.
- A step that legitimately prints nothing for 15 minutes and is then cancelled by a newer push would be filed
  as a hang; the task it files names the silence, so a lane can tell and close it.
