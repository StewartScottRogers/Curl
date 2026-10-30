---
id: BL-4366
title: Split Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.Authentication.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.Channel.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.HostKeys.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.Transfer.cs, Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.Transport.cs]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4366 — Split Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs so tasks touching it can run in parallel

## Goal

`Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 2 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs`: BL-4329, BL-4330, BL-4362, BL-4363, BL-4364, BL-4365. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-4329, BL-4330, BL-4362, BL-4363, BL-4364, BL-4365) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Protocol.Ssh.UnitLibrary/SshDiagnosticLog.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `SshDiagnosticLog` is one class, not a list of cases, so its units are its groups of
  log steps: it is now a `partial` class whose core file keeps the constructor, `None` and
  the `Write` helpers, and one file per group holds that group's methods and private
  formatter - `SshDiagnosticLog.Transport.cs` (message numbers, handshake, key exchanges),
  `.HostKeys.cs`, `.Authentication.cs`, `.Channel.cs` (session channel and SFTP) and
  `.Transfer.cs` (start, end, failure). A partial file "registers itself" by compiling into
  the class; no central list to edit. Pure move: no line of behaviour changed. Chosen
  because the waiting tasks move code group by group into `Curl.Ssh.UnitLibrary`.
- Touches widened to the five new files, which this task creates.
- Waiting tasks: BL-4363 (Deferred) named the file itself; its `touches` now names all six
  parts. BL-4329, BL-4330 and BL-4362 name the whole `Curl.Protocol.Ssh.UnitLibrary`
  because they move code across it, so narrowing them to these files would be unsafe (too
  narrow lets two lanes edit one file); left as they are. BL-4364 and BL-4365 are Deferred,
  which `hotspots` does not count. `task-board.ps1 hotspots -Min 1` now counts 4 tasks for
  each SshDiagnosticLog file (BL-4329, BL-4330, BL-4362 and this task), 3 once this is Done;
  plain `hotspots` reports none.
- `dotnet build` clean (0 warnings), fast tests green (112 test projects passed, exit 0),
  `dotnet format --verify-no-changes` clean, `Measure-CodeQuality.ps1 -Library
  Curl.Protocol.Ssh.UnitLibrary`: 0 failing members.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 8d1bd4dd 649dac37 a0b49a29.
- 2026-10-30: Doing -> Done. SshDiagnosticLog is split into one partial file per group of log steps, so tasks can touch one group's file
