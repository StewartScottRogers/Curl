# ADR-1170: A coordinator whose start-up sync brings in factory code runs itself again in the same process

- Status: Accepted
- Date: 2026-10-31
- Task: BL-4386
- Decided by Claude under Stewart's delegation.

## Context

The shift coordinator dot-sources `RunDarkFactory.ps1`'s area files under `DarkFactory/`
when its process starts, and only then runs the shift-start sync
(`Sync-CheckoutWithOrigin`), which may fast-forward the checkout. On 2026-10-31 a
`-Restart` straight after the push of BL-4384's CI-watch fix fast-forwarded to it and
still ran the code from before it; a second `-Restart` was needed.

BL-4386 allowed either the coordinator starting itself again with the same arguments, in
the same herdr tab or console and never with `Start-Process`, or `-Restart` and `-NewTab`
fast-forwarding the checkout before they launch it.

## Decision

The coordinator does it itself. `Invoke-ShiftStartSync` (`DarkFactory/ShiftCoordinator.ps1`)
notes HEAD before and after the sync; when HEAD moved and
`git diff --name-only` over the commits it brought in names `RunDarkFactory.ps1` or a file
under `DarkFactory/`, the coordinator traces one line
(`sync    factory code changed <old>..<new>; restarting on it`), removes its `git` and
`gh` timeout wrappers, and runs `& $FactoryScript` with its own bound parameters plus
`-ShiftStamp` of the shift it began. That happens before anything is claimed, merged or
watched. The new run re-reads every file from disk, its own sync finds nothing to bring in,
and the shift goes on in the same trace log. A sync that brings in no factory code starts
the shift as before, with no extra line.

## Why

- It covers every way a coordinator starts (`RunDarkFactory.cmd`, `-Restart`, `-NewTab`,
  the next `-Continuous` shift, a watchdog replacement), where a fix in the launchers
  covers only two of them.
- Running the script again in the same process keeps the pid, the command line and the
  console. So the coordinator stamp, the watchdog and the way `-Restart` finds the
  coordinator by its command line all keep working, and nothing new needs a process of its
  own.
- Keeping `-ShiftStamp` means one shift writes one trace log. A second log would need a
  `shift end` line in the first one (AF-0313).

## Consequences

The old run's script scope stays on the call stack under the new run until the shift ends.
Its functions are shadowed by the new run's, which define the same names in their own
script scope. The only things that would outlive that scope are the `git` and `gh`
wrappers, and those are removed first. `-TestCoordinatorWatchdog` checks the decision on a
throwaway repository with a stand-in for the sync.
