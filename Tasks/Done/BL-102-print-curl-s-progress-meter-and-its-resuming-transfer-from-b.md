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
completed: 2026-09-26
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
- [x] A test in `Curl.Console.UnitTests` pins that `-C 5` writes `** Resuming transfer from byte position 5` to stderr, before the meter.
- [x] Tests in `Curl.Console.UnitTests` pin that neither the resuming line nor the meter appears under `-s` and under `--no-progress-meter`.
- [x] A test pins the meter's header lines as recorded in Notes.
- [x] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no new test needs `TestCategory=Integration`.

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
- 2026-09-26, lane 3 (delivery): `Curl.Console/ProgressMeterLines.cs` holds the recorded lines; `CurlCommandRunner` writes them through its existing stderr line writer (so `Environment.NewLine`, which on Windows gives the `\r\n` curl's mingw build writes) after each transfer. Tests: `CurlCommandRunnerProgressMeterTests` (16), plus `CurlCompositionTests.CreateRunner_FileUrlToStandardOutputThatIsNotATerminal_WritesTheProgressMeter`. The built `curl` was compared with `cmp` against curl 8.21.0 for `-C 5 file://…ten.bin -o m2` and `file://…ten.bin > o`: stderr and output byte-identical.
- Choices taken as defaults, and why:
  - The meter is written after the transfer, not during it. It goes to stderr and the body does not, so each stream's bytes are unchanged; nothing timing-dependent is involved, so no `TimeProvider` is needed yet.
  - The meter follows only a successful transfer. Measured: curl writes it when the transfer got past connect/open (`-C 5` against an HTTP server without ranges prints the meter, then `curl: (33) …`) but not before (a missing `file://` source prints only `curl: (37) …`). The console cannot tell those apart until handlers report that a transfer started: BL-128/BL-130.
  - Only the zero status line is written. For network transfers curl rewrites it in place with live counters (measured against a local `python -m http.server`); that needs handler progress reporting: BL-131.
  - Under `-#` nothing is written rather than the wrong form: BL-132.
  - With no `-o` and standard output a terminal, the meter is hidden, as curl hides it; `Program` passes `!Console.IsOutputRedirected`. The runner writes the meter only when constructed with `writesProgressMeter: true` (the production composition), so the existing runner tests keep asserting stderr without it.
- Follow-ups filed by task-planner: BL-127 (ADR for a progress sink), BL-128 (sink in Abstractions), BL-129 (file:// reports "started"), BL-130 (meter after a failure past connect), BL-131 (live counters), BL-132 (`-#` bar).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 1; see logs\BL-102-20260926-083111-L4.jsonl
- 2026-09-26: Blocked -> Backlog. Not blocked: the 2026-09-26 shift ran out of tokens (usage limit), which it misfiled as a stall
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Waits on BL-119: Curl.Cli does not parse --no-progress-meter or -#/--progress-bar yet; move back to Backlog when BL-119 is Done
- 2026-09-26: Blocked -> Backlog. Unblocked: BL-119 now Done
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Curl.Console writes curl 8.21.0's progress meter opening and its '** Resuming transfer from byte position N' line to stderr, byte-identical for file://, and hides both under -s and --no-progress-meter
