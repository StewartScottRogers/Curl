---
id: BL-102
title: Print curl's progress meter and its '** Resuming transfer from byte position N' line in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-095, BL-119]
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

- [x] Before any code is written, the exact stderr bytes of curl 8.21.0's meter for a ten-byte `file://` download (with and without `-C 5`) are recorded under Notes in this file, with the command that produced them.
- [ ] A test in `Curl.Console.UnitTests` pins that `-C 5` writes `** Resuming transfer from byte position 5` to stderr, before the meter.
- [ ] Tests in `Curl.Console.UnitTests` pin that neither the resuming line nor the meter appears under `-s` and under `--no-progress-meter`.
- [ ] A test pins the meter's header lines as recorded in Notes.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`.

## Notes

- 2026-09-26, lane 3: recorded curl 8.21.0 (x86_64-w64-mingw32) stderr for a ten-byte `file://` source (`printf '0123456789' > ten.bin`), with stdout redirected to a file and stderr to a file (not a terminal). `^M$` is `
` as `cat -A` shows it; the leading `^M` of the third line is a lone ``.
  - `curl file:///Z:/tmp-bl102/ten.bin -o o1 2>e1.txt` (exit 0):
    ```
      % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current^M$
                                     Dload  Upload  Total   Spent   Left   Speed^M$
    ^M  0      0   0      0   0      0      0      0                              0^M$
    ```
  - `printf '01234' > o2; curl -C 5 file:///Z:/tmp-bl102/ten.bin -o o2 2>e2.txt` (exit 0, `o2` becomes `0123456789`):
    ```
    ** Resuming transfer from byte position 5^M$
      % Total    % Received % Xferd  Average Speed  Time    Time    Time   Current^M$
                                     Dload  Upload  Total   Spent   Left   Speed^M$
    ^M  0      0   0      0   0      0      0      0                              0^M$
    ```
  - The `
` endings are the mingw build's text-mode stderr; the curl sources write `
`. Whether Curl.Console should match these bytes or `
` is for whoever implements, following how Curl.Console already writes its other stderr lines.
  - Note the file:// status line reports zeros throughout, even though ten bytes were transferred.
- 2026-09-26, lane 3: blocked on the parser. `Curl.Cli.UnitLibrary` still has no `progress` option (grep), so `--no-progress-meter` exits 2 as unknown and the third criterion cannot be tested from Curl.Console. As the Context instructs, filed BL-119 (touches `Curl.Cli.UnitLibrary`, `Curl.Cli.UnitTests`) and added it to `depends-on`; no code was written here.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 1; see logs\BL-102-20260926-083111-L4.jsonl
- 2026-09-26: Blocked -> Backlog. Not blocked: the 2026-09-26 shift ran out of tokens (usage limit), which it misfiled as a stall
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Waits on BL-119: Curl.Cli does not parse --no-progress-meter or -#/--progress-bar yet; move back to Backlog when BL-119 is Done
- 2026-09-26: Blocked -> Backlog. Unblocked: BL-119 now Done
