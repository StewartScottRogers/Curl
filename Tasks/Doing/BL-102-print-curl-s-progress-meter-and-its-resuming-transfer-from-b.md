---
id: BL-102
title: Print curl's progress meter and its '** Resuming transfer from byte position N' line in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-095]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-102 — Print curl's progress meter and its '** Resuming transfer from byte position N' line in Curl.Console

## Goal

Without `-s` or `--no-progress-meter`, Curl.Console writes curl's progress meter to stderr, preceded under `-C N` (N > 0) by `** Resuming transfer from byte position N`, with the same bytes as curl 8.21.0.

## Context

Found while finishing BL-095. Measured with local curl 8.21.0 (x86_64-w64-mingw32) on Windows, 2026-09-26, against a ten-byte `file://` source:

- Without `-s` or `--no-progress-meter`, curl writes the progress meter to stderr whenever output is not a terminal: the `  % Total    % Received % Xferd  Average Speed ...` header lines and a status line.
- With `-C N` (N > 0) it writes `** Resuming transfer from byte position N` on stderr before the meter.
- `-s`/`--silent` and `--no-progress-meter` suppress both.

Curl.Console prints neither today. `Curl.Cli.UnitLibrary` has `CommandLineOptions.Silent` but, at filing time, no `--no-progress-meter` or `-#`/`--progress-bar` option (grep found no `progress` in `Curl.Cli.UnitLibrary`). Upstream reference: https://curl.se/docs/manpage.html (`--no-progress-meter`, `-#, --progress-bar`, `-C, --continue-at`, `-s, --silent`).

**Confirm touches during planning.** If the parser needs `--no-progress-meter` (and `-#`) added, that is `Curl.Cli.UnitLibrary`/`Curl.Cli.UnitTests` work: file it as its own task with `task-planner`, add it to this task's `depends-on`, move this task to `Blocked` on it, and do not widen this task. Keep the timing-dependent parts of the meter behind the injected `TimeProvider`; never `Thread.Sleep`.

## Acceptance criteria

- [ ] Before any code is written, the exact stderr bytes of curl 8.21.0's meter for a ten-byte `file://` download (with and without `-C 5`) are recorded under Notes in this file, with the command that produced them.
- [ ] A test in `Curl.Console.UnitTests` pins that `-C 5` writes `** Resuming transfer from byte position 5` to stderr, before the meter.
- [ ] Tests in `Curl.Console.UnitTests` pin that neither the resuming line nor the meter appears under `-s` and under `--no-progress-meter`.
- [ ] A test pins the meter's header lines as recorded in Notes.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 1; see logs\BL-102-20260926-083111-L4.jsonl
- 2026-09-26: Blocked -> Backlog. Not blocked: the 2026-09-26 shift ran out of tokens (usage limit), which it misfiled as a stall
- 2026-09-26: Backlog -> Doing.
