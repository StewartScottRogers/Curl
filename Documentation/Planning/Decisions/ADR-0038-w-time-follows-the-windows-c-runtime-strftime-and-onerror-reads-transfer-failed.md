# ADR-0038 — `-w %time{…}` follows the Windows C runtime's `strftime`, and `%{onerror}` reads `TransferFailed`

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

curl 8.21.0's `tool_writeout.c` renders `%time{format}` by taking the current time as
UTC, replacing `%f` with the microseconds, `%z` with `+0000` and `%Z` with `UTC` itself,
and handing the rest to the C runtime's `strftime` with a 256-byte buffer. What the rest
does is therefore the C runtime's, which differs by platform: the Windows (mingw) build
uses the Microsoft C runtime, the Linux and macOS builds use glibc or the BSD libc. The
tool also calls `setlocale(LC_ALL, "")`, so names and the `%c`, `%x`, `%X` layouts follow
the machine's locale.

`%{onerror}` ends the output when the transfer succeeded and prints nothing otherwise
(BL-279). The renderer only saw variables through `IWriteOutVariableSource`, which could
not say whether the transfer failed.

Measured with the reference build (curl 8.21.0, mingw, Schannel, en-US Windows, US
Mountain Standard Time) on 2026-09-26, `curl -s -o NUL -w "[%time{…}]" file:///c:/Windows/win.ini`:

| Format | Printed |
| --- | --- |
| `%a %A %b %B %d %H %I %j %m %M %p %S %U %w %W %y %Y %%` | as C89 `strftime` defines them |
| `%c`, `%x`, `%X` | `9/27/2026 3:30:08 AM`, `9/27/2026`, `3:30:08 AM` |
| `%#c`, `%#x` | `Sunday, September 27, 2026 3:30:08 AM`, `Sunday, September 27, 2026` |
| `%#d`, `%#H`, `%#m` … | the number without leading zeros |
| `%#z`, `%#Z` | `US Mountain Standard Time` |
| `%f`, `%z`, `%Z`, `%s` | `545957`, `+0000`, `UTC`, the Unix seconds |
| `%%f` | `%f` |
| `%C %D %e %F %g %G %h %n %r %R %t %T %u %V %k %l %P %E %O %q %Ey %#f %#s`, a trailing `%` | nothing: the whole format prints nothing |
| 255 bytes of literal text / 256 bytes | the text / nothing |

`a%{onerror}b%{stderr}c` printed `a` for a transfer that worked and `abc` for one that
exited 37.

## Decision

- `WriteOutTimeFormatter` implements the Windows C runtime's conversions as measured,
  including the `#` flag, and returns nothing for any conversion that runtime rejects or
  for a result of 256 bytes or more.
- Names and the `%c`, `%x`, `%X` layouts are the United States English ones, fixed rather
  than taken from the current culture: `Curl.Console` publishes with
  `InvariantGlobalization`, where the current culture is invariant and its layouts
  (`MM/dd/yyyy HH:mm:ss`) match no curl. The local time zone's standard name for `%#z`
  and `%#Z` comes from the injected `TimeProvider.LocalTimeZone`.
- The time comes from a `TimeProvider` passed to `WriteOutTemplateRenderer`, read once
  per `%time{…}`, as curl reads the clock once per directive.
- `IWriteOutVariableSource` gains `TransferFailed`; `TransferWriteOutVariables` answers it
  from `TransferResult.IsSuccess`.
- The glibc dialect the Linux and macOS builds print is not implemented yet; it is filed
  as its own task.

## Consequences

- On Windows, `%time{…}` matches curl byte for byte for an en-US machine. On a machine
  with another locale, curl prints localised names and layouts and Curl prints English.
- On Linux and macOS, formats that only glibc knows (`%F`, `%T`, `%e`, …) print nothing
  until the glibc dialect lands.
