# ADR-0819: The Windows password prompt reads the console input buffer, not standard input

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3573
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0's Windows `getpass_r` (`src/tool_getpass.c`) reads the `-u` and `--proxy-user`
password with the CRT's `_getch`, which reads the console input buffer (`CONIN$`) with every input
mode turned off, whatever standard input is. Measured with curl 8.21.0 Schannel on Windows:
`curl -s -u user http://127.0.0.1:20002/ < pw.txt` and `... < /dev/null` both print
`Enter host password for user 'user':` and wait for the console until killed (exit 124 under an
8 s `timeout`). `ConsolePasswordPrompt` read keys with `Console.ReadKey(intercept: true)`, which
throws `InvalidOperationException` when standard input is redirected, so the password came back
empty at once and the request went out with `user:` (CGR-044).

## Decision

- On Windows `ConsolePasswordPrompt.ForPlatform` reads keys through `WindowsConsolePasswordInput`:
  `CONIN$` opened once with `CreateFileW` (read and write access, read and write sharing), and for
  each key the console's modes saved with `GetConsoleMode`, set to none with `SetConsoleMode`, one
  character read with `ReadConsoleW` and the modes put back, as `_getch` does. Standard input is
  never read, redirected or not.
- With no console to open or read, `ReadKey` throws `InvalidOperationException` and the prompt keeps
  what was typed, as before.
- The BCL has no raw console-buffer read that ignores a redirected standard input, so this is a
  thin native adapter left out of the coverage measure per ADR-0083.
- Special keys (arrows, function keys) give no character to `ReadConsoleW`, where `_getch` returns a
  two-character code; that difference only matters to a password typed with those keys, and is left.

## Consequences

A script that pipes a password into `curl -u user` waits for the console, as real curl does on
Windows; scripts must give the password in `-u user:password` or a `.netrc`, as with curl.
