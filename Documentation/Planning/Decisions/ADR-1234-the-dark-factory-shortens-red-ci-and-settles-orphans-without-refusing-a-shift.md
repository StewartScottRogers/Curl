# ADR-1234: The dark factory shortens red CI and settles orphans without refusing a shift

- Status: Accepted
- Date: 2026-11-03
- Tasks: BL-4647, BL-4648, BL-4649, BL-4650, BL-4715, BL-4716, BL-4717
- Decided by Claude under Stewart's delegation

## Context

The audit of 2026-11-02 (audit-process findings AF-0541 to AF-0544 and AF-0567 to AF-0569) measured
`work/dark-factory` staying red for 35 to 167 minutes at a time, and a shift start refused for 32 minutes:

- The CI watch looked only for a run whose status was `in_progress`. GitHub reports the run holding the
  branch's slot as `queued` while any of its jobs waits for a runner (macOS capacity), so a run whose Linux
  job had already failed was neither read early nor cancelled.
- Runs that already held a fix were cancelled for lacking CI-fix tasks that had closed with nothing to change.
- Duplicate CI-fix tasks were filed for one failing test, and the watch read finished runs every 3 minutes.
- A lane integrated a library change that broke the Windows quality gate, which only CI then measured.
- A task in Doing that no lane held (BL-2969) was given to the wrong lane, and when the owner could not be
  told the shift refused to start, which also kept the CI watch off.

## Decision

- **CI watch** (`DarkFactory/CiWatch.ps1`): the oldest unfinished run that is not `pending` counts as the
  running one, `queued` included. A CI-fix task does not count as a missing fix when its Done line names a
  commit the running run already contains. While the newest finished run is red the watch reads every minute,
  not every `-HeartbeatMinutes`.
- **Claim** (`DarkFactory/Claim.ps1`): a CI-fix task whose failing test method is already named by a live
  CI-fix task in Doing, or an older one in Backlog, is deferred as a duplicate when claimed.
- **Integrate** (`DarkFactory/Integrate.ps1`, `DarkFactory/Prompts.ps1`): before taking the integrate lock, a
  lane runs `Measure-CodeQuality.ps1` for the gated libraries its commits change. A member of a changed file
  outside a gate gives a finished task one repair run; otherwise the work is parked. It runs outside the lock,
  so other lanes do not wait.
- **Shift start** (`DarkFactory/Park.ps1`, `DarkFactory/ShiftCoordinator.ps1`): a fresh shift never reads its
  own `lanes-<stamp>` folder as the previous shift's (7ecfdd99f). A task in Doing held by no lane is adopted
  only by a lane whose newest run log names it. When its owner still cannot be told, the task goes to
  `Blocked` (lane branches with unpushed commits copied first to `factory/<ID>-lane-<n>-<stamp>`, named in the
  reason) and the shift starts, instead of refusing.

## Consequences

Each integration that changes a gated C# file costs one library measurement. A shift no longer stops on one
unclear orphan; Stewart or an interactive session settles it from `Blocked`. The seven tasks close only after
the audit's measurement over a shift that ran this code shows the red-CI time and the refusals gone.
