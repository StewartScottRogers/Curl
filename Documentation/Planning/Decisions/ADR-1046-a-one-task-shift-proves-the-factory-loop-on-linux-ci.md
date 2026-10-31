# ADR-1046: A one-task shift proves the dark factory loop on Linux CI

- Status: Accepted
- Date: 2026-10-29
- Task: BL-3933
- Decided by Claude under Stewart's delegation.

## Context

The factory's self-tests run on Linux and macOS (BL-3923), but nothing ran a whole shift
there. Every `claude -p` run the factory starts went through `cmd.exe /d /c` (`$env:ComSpec`),
and runs that overran were stopped with `taskkill`, so a shift on Linux could not start a run.

## Decision

- `Set-FactoryShellCommand` (`DarkFactory/Platform.ps1`) starts a command line through
  `cmd.exe /d /c` on Windows and `/bin/sh -c` elsewhere; `Get-FactoryNullDevice` gives `nul`
  or `/dev/null`. Every claude run (task runs, splits, shift review, token probes) uses it,
  and every overrun is stopped with `Stop-FactoryProcessTree`, never `taskkill` directly.
- `DarkFactory/ShiftProof.ps1` runs a real shift (`-Lanes 2 -MaxTasks 1`) in a clone of the
  commit under test whose `origin` is a local bare repository and whose board holds one
  scratch task, with a stand-in `claude` first on `PATH`. It needs no new
  `RunDarkFactory.ps1` parameter: the remote is whatever `origin` is, and `claude` is found
  on `PATH`. The `origin` is re-created rather than re-pointed, so no remote-tracking
  `master` is left and the shift's merge step finds nothing to merge instead of waiting for
  GitHub's CI.
- The proof judges the shift by what reached `origin` (the task in `Done`, the stand-in's
  file and commit), not by the shift's exit code, which also reflects audit and alarm
  notices that a scratch board says nothing about.
- `.github/workflows/dark-factory-unix-shift.yml` runs the proof on `ubuntu-latest` on
  pushes to `work/dark-factory` and on demand.

## Consequences

A change that breaks the claim, run, integrate or board loop off Windows turns that
workflow red. The same script runs on Windows, where it was proved before the workflow
landed. The shift's CI watch reads only the `CI` workflow, so a red run of this one is seen
only on GitHub.
