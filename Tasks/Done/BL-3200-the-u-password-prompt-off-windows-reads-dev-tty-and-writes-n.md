---
id: BL-3200
title: The -u password prompt off Windows reads /dev/tty and writes no newline when echo was not disabled, as curl 8.22.0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs, Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs, Curl.Cli.UnitLibrary/PosixTerminalPasswordInput.cs, Curl.Cli.UnitLibrary/README.md]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3200 — The -u password prompt off Windows reads /dev/tty and writes no newline when echo was not disabled, as curl 8.22.0

## Goal

Off Windows, `-u user` with no password reads the password the way curl 8.22.0's POSIX `getpass_r` does: from `/dev/tty` when it opens (else standard input), with echo turned off, and the LF after the entry is written only when echo was actually turned off.

## Context

Found by BL-3187. Upstream 8.22.0 `src/tool_getpass.c` (non-Windows): `fd = open("/dev/tty", O_RDONLY)`, falling back to `STDIN_FILENO`; `ttyecho(FALSE, fd)` turns echo off and reports whether it did; `fputs(prompt, tool_stderr)`; `read(fd, ...)`; and `if(disabled) fputs("\n", tool_stderr)`. `Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs` uses `Console.ReadKey` and always writes the line terminator, so when standard input is redirected and no terminal is open it writes an LF that curl does not. On a terminal the bytes already match (BL-3187 pins them). Windows is unaffected: its `getpass_r` always writes the newline.

## Acceptance criteria

- [x] A test in `Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs`, run off Windows only, pins that no LF follows the prompt when echo could not be turned off, and that the LF follows when it could.
- [x] Curl.Cli.UnitLibrary builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

- Decided (ADR-0771): `ConsolePasswordPrompt` takes `turnEchoOff`/`turnEchoOn`; the line terminator and the echo restore happen only when echo was turned off. The two-argument constructor keeps Windows behaviour. Off Windows `ForPlatform(false, ...)` reads through the new `PosixTerminalPasswordInput` (`/dev/tty`, else standard input; `tcgetattr`/`tcsetattr` from libc on the `ECHO` bit), a thin native adapter excluded from coverage per ADR-0083, like `UserDatabaseHomeDirectory`.
- Added `Curl.Cli.UnitLibrary/PosixTerminalPasswordInput.cs` (new file) and `Curl.Cli.UnitLibrary/README.md` (its `ConsolePasswordPrompt` row described the old behaviour) to touches; no task in Doing on origin/work/dark-factory names either.
- `ForProcessConsole_InputRedirected_ReturnsAnEmptyPassword` is now Windows only: off Windows the prompt reads standard input to its end, which would wait on the test host's input.
- Off-Windows tests are platform-neutral by construction (scripted keys and echo control, StringWriter); they and a platform-neutral data-row test cover both branches. CI on Linux and macOS is left to the shift's CI watch.
- Measured: Curl.Cli.UnitTests 4132 passed, 22 skipped, 0 failed; Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 0f02880e 60a3084c.
- 2026-10-22: Doing -> Done. Off Windows the -u password prompt reads /dev/tty (else stdin) with echo off and writes the LF only when echo was turned off, as curl 8.22.0
