---
id: BL-1318
title: Measure-CodeQuality -Library runs only the test projects that reach the library
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Measure-CodeQuality.ps1, .claude/skills/task-run]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1318 — Measure-CodeQuality -Library runs only the test projects that reach the library

## Goal

`Measure-CodeQuality.ps1 -Library <name>` runs only the test projects that reach that library, so a lane's coverage check takes minutes instead of most of its time limit, and no lane stops another lane's run.

## Context

- 2026-10-03: BL-1293, BL-1296, BL-1297, BL-1301 and BL-1304 timed out after 120 minutes in the 21:10 shift. Each had its code written and its own tests green within 15-45 minutes. The rest went to `Measure-CodeQuality.ps1 -Library`, which filtered only the report but ran `dotnet test Curl.slnx` with coverage over about 25,000 tests: 40-58 minutes a run on nine lanes. BL-1306 (01:16:21) and BL-1307 (03:13:19) also stopped every process on the machine whose command line held `Measure-CodeQuality`, killing other lanes' runs.
- Coverage is merged across test projects (a library's code run from another project's tests counts), so the run must include every test project whose references reach the library, directly or through other projects, not only its twin.

## Acceptance criteria

- [x] With `-Library`, the script runs one `dotnet test` per `*.UnitTests` project whose ProjectReference graph reaches a matching project, into the same results directory; without it, or when none matches, the whole solution as before.
- [x] `-Library Curl.Protocol.Tftp.UnitLibrary` ran Conformance, Console and TFTP's test projects and finished in 95 s on a busy machine (nine lanes running), against 40-58 min before; it reported the real TFTP gap (the `DefaultPort` fallback branch) that BL-1304's stashed work covers.
- [x] `.claude/commands/task-run.md` says to measure one library with `-Library`, and to stop only processes the run started, by PID.
- [x] The script's help for `-Library` describes the new behaviour.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-03: The five timed-out tasks went back to Backlog with notes naming their shared stash by hash and the `git checkout <hash> -- <files>` that restores their work.
- Touched `.claude/commands/task-run.md`, where the task-run instructions live (the task's `touches` said `.claude/skills/task-run`).
## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Measure -Library runs only the test projects reaching the library (95 s, not 40-58 min); task-run stops only its own processes
