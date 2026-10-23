# ADR-0775: The POSIX password prompt reads /dev/tty and writes the LF only when echo was off

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3200
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0's non-Windows `getpass_r` (`src/tool_getpass.c`) opens `/dev/tty`, falling back to
standard input, turns echo off with `ttyecho(FALSE, fd)` (which reports whether it could),
writes the prompt to stderr, reads the password, and writes `"\n"` and turns echo back on only
`if(disabled)`. `ConsolePasswordPrompt` read keys with `Console.ReadKey` everywhere and always
wrote the line terminator, so off Windows with no terminal it wrote an LF curl does not, and with
standard input redirected it could not read the terminal curl reads.

## Decision

- `ConsolePasswordPrompt` takes a `turnEchoOff` (returns whether it did) and a `turnEchoOn`; the
  line terminator is written, and echo turned back on, only when echo was turned off. Its
  two-argument constructor keeps the Windows behaviour (echo always off, terminator always
  written), which matches Windows curl's `getpass_r`.
- Off Windows, `ConsolePasswordPrompt.ForPlatform` reads through `PosixTerminalPasswordInput`:
  `/dev/tty` when it opens, else standard input, with echo changed by `tcgetattr`/`tcsetattr`
  from `libc` (only `c_lflag`'s `ECHO` bit, at byte 12 of glibc's `termios` and byte 24 of
  Darwin's), restored with `TCSAFLUSH` as curl does. The BCL has no terminal-settings API, so this
  is a thin native adapter left out of the Windows coverage measure per ADR-0083.
- Characters are read until CR or LF, or the end of the input. curl does one `read()` and drops
  its last byte; for a line ending in LF the password is the same, and a terminal in canonical
  mode delivers one line per read.

## Consequences

Off Windows, a password piped on standard input with no controlling terminal now reads that
input (before it read nothing) and writes only the prompt, as curl does. The unit tests pin the
bytes with a scripted echo control; the native adapter runs only off Windows and no fast test drives it, since reading a terminal would wait for a key.
