---
id: BL-5109
title: Stop the Linux password prompt writing keypad-transmit escapes
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/PosixTerminalPasswordInput.cs, Curl.Cli.UnitTests/PosixTerminalPasswordInputBytesTests.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5109 — Stop the Linux password prompt writing keypad-transmit escapes

## Goal

The `-u user` / `-U user` password prompt on a Linux terminal writes only the prompt text and the trailing `\r\n` to the terminal, and leaves no terminal mode changed.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-062 (Low, Probable: the escape sequence is Confirmed twice; that the terminal stays changed is inferred). Area: CLI, `-u user` / `-U user` prompt on a TTY, Linux build.

**Repro (Linux).** Run `curl -sS -u alice http://127.0.0.1:25080/` under a pty (a PowerShell or C# file-based harness, not Python) and type a password; record the bytes written to the pty.

**Expected (upstream curl on Linux).** The pty receives `Enter host password for user 'alice':` and, after input, `\r\n`; nothing else.

**Actual.** The pty receives `\x1b[?1h\x1b=Enter host password for user 'alice':\r\n`, and no `\x1b[?1l\x1b>` reset follows before exit. The password and Authorization header are correct.

**Where to start.** `Curl.Cli.UnitLibrary/PosixTerminalPasswordInput.cs`; .NET `System.Console` terminfo setup writes `keypad_xmit`. Read the password through `/dev/tty` with termios directly, or emit `keypad_local` afterwards. The BCL-only rule applies (no package); native calls go through the P/Invoke style already used in the solution (AOT-safe).

## Acceptance criteria

- [x] New tests in `Curl.Cli.UnitTests/PosixTerminalPasswordInputBytesTests.cs` prove, with a recording terminal stand-in, that the bytes written before the password are exactly `Enter host password for user 'alice':` and the bytes after are exactly `\r\n`, with no `\x1b[?1h` or `\x1b=` sequence.
- [x] A test proves the terminal's echo setting is restored after a normal read and after a cancelled one.
- [x] Tests are platform-neutral (stand-in terminal) or marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- A harness that runs curl under a pty must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md); a pty run is an Integration-style hand check, not a unit test.
- Review acceptance check, as a hand check on Linux under a pty: the bytes written before the password are exactly `Enter host password for user 'alice':`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: ca784fa4 35434768 ea4464e1 a0b49a29.
- 2026-11-13: Cause: Console.Error's first use runs .NET terminfo setup (keypad_xmit). Fix: off Windows the prompt writes to fd 2 through ProcessStandardError (no Console.Error). Tests recorded in PosixTerminalPasswordInputBytesTests. Echo restore on cancel is by ReadKeysUntilEnter keeping input. Linux pty hand check not run (Windows lane); platform-neutral tests.
- 2026-11-13: Doing -> Done. Prompt writes via fd 2, no keypad escapes; tests added
