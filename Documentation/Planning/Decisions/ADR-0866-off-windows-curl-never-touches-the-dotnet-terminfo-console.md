# ADR-0866 — Off Windows Curl never touches .NET's terminfo console

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3644 (CGR-115)
- Decided by Claude under Stewart's delegation.

## Context

The third-party gap report (CGR-115) found that on Linux Curl writes `ESC[?1h ESC=` (terminfo's
`keypad_xmit`, application cursor and keypad mode) to the terminal on every run where a standard
stream is a terminal, even under `-s`, with standard output redirected and for `--version`, and
never resets it. curl writes no control sequence.

On Unix .NET's `ConsolePal.EnsureConsoleInitialized` writes `keypad_xmit` to the terminal. It runs
when `Console.WindowWidth` is read and on every write through a `UnixConsoleStream`
(`ConsolePal.WriteFromConsoleStream`). Curl did all three: `TerminalColumns` read
`Console.WindowWidth` for the warning wrapper's width, `Program` wrote standard error through
`Console.OpenStandardError()`, and `StandardOutputOpener` wrote a terminal standard output through
`Console.OpenStandardOutput()`. Reads through `Console.OpenStandardInput()` do not initialise it.

## Decision

Off Windows Curl never reads `Console.WindowWidth` and never writes through a .NET console stream:

- `TerminalColumns` reads the width as curl's `get_terminal_columns` does there,
  `ioctl(STDIN_FILENO, TIOCGWINSZ)`, through `NativeTerminalWindowSize`, which calls the runtime's
  own `SystemNative_GetWindowSize` shim in `libSystem.Native` (fixed signature; libc's `ioctl` is
  variadic, which a plain import cannot call on macOS arm64). It is left out of the Windows
  coverage measure as ADR-0083 allows for a thin native adapter.
- `StandardOutputOpener` opens a terminal standard output over descriptor 1 as the unbuffered
  `FileStream` it already used for a redirected one.
- `StandardErrorOpener` opens standard error over descriptor 2 the same way (still inside
  `WriteFailureIgnoringStream`).

On Windows nothing changes: the console streams and `Console.WindowWidth` (minus one) stay, as the
Windows console needs `WriteConsoleW` and has no terminfo.

## Consequences

Measured under `script` with `TERM=xterm` in WSL Ubuntu against curl 8.18.0: every CGR-115 repro
prints the same bytes as curl, with no control sequence (BL-3644 Notes). Off Windows the warning
wrapper's width now follows the terminal on standard input, as curl's does, not standard error's.
