# ADR-0078 — `-w %time{…}` on Linux and macOS follows glibc's `strftime` in the C locale

- **Status:** Accepted
- **Date:** 2026-09-27
- **Extends:** ADR-0038, which covers the Windows dialect

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

ADR-0038 made `%time{format}` follow the Windows C runtime and left the Linux and macOS
builds, which hand the format to their own libc, for later. The standing rule is to match
the OpenSSL build of curl there.

curl 8.21.0's `outtime` (`src/tool_writeout.c`) rewrites the format before `strftime`
sees it: `%f` becomes the six-digit microseconds, `%s` the Unix seconds, `%z` `+0000`,
`%Z` `UTC`, and `%%` stays `%%`. It then calls `strftime` with the UTC time from
`gmtime` and a 256-byte buffer, and prints nothing when the result does not fit. (8.18.0,
the curl Ubuntu ships, did not yet rewrite `%s` or keep `%%`: `%%f` printed nothing there.)

Measured on 2026-09-27 with curl 8.21.0 built from its release tarball with
`--enable-debug` on Debian trixie (glibc 2.41), so `CURL_TIME` fixes the clock:
`CURL_TIME=<seconds> curl -s -o /dev/null -w '%time{<format>}' file:///tmp/f`. 1,593
formats (every letter and several punctuation marks, each under 26 combinations of flags,
widths and modifiers, plus edge cases) at three instants are in
`Curl.Output.UnitTests/Fixtures/glibc-time-format.json`. What they show:

| Case | Printed |
| --- | --- |
| `%c`, `%x`, `%X`, `%r` | `Sun Sep 27 03:33:20 2026`, `09/27/26`, `03:33:20`, `03:33:20 AM` |
| `%C %D %e %F %g %G %h %k %l %n %P %R %t %T %u %V` | as glibc documents them |
| `_`, `-`, `0` | fill with spaces, no fill, zeros; the last one written wins |
| `^`, `#` | `^` upper cases; `#` upper cases `%a %A %b %B %h` and lower cases `%p %P %Z` |
| a width, as in `%10Y`, `%5a` | the field is that wide; numbers fill with zeros, text with spaces |
| `E`, `O` | accepted where glibc accepts them (`%Ey`, `%Od`, …) and the C locale's plain value |
| an unknown directive, `%q`, `%Ed`, `%5` | copied as written, padded to its width and upper cased under `^` |
| a trailing `%`, `%#` | copied as written |
| `%Z` that curl did not rewrite (`%-Z`, `%#Z`) | `GMT`, `gmt` |
| `%s` that curl did not rewrite (`%-s`, `%12s`) | the UTC time read as local standard time, as `mktime` reads it: `TZ=JST-9` gives 9 hours less |
| 255 bytes / 256 bytes | the text / nothing |

## Decision

- `WriteOutTimeDialect` names the two dialects, `WindowsCRuntime` and `Glibc`.
  `WriteOutTimeFormatter.Format` takes one and hands the format to
  `WindowsCRuntimeTimeFormat` (ADR-0038) or `GlibcTimeFormat`.
- `GlibcTimeFormat` ports curl's rewrite and then glibc's `strftime` for the C locale, as
  measured. It is checked against every row of the fixture.
- `WriteOutTemplateRenderer` takes the dialect as a constructor argument. Its
  three-argument constructor keeps the Windows dialect so `Curl.Console`, which BL-131 had
  in progress, builds unchanged; BL-383 makes `Curl.Console` pass `Glibc` off Windows and
  removes that constructor.
- The C locale, not the machine's: `Curl.Console` publishes with `InvariantGlobalization`,
  as ADR-0038 says for Windows, and the C locale is what curl prints with no `LANG` set.
- macOS uses the same dialect. Its curl calls the BSD libc's `strftime`, which was not
  measured; glibc is the nearest measured match and the two agree on the C89 and POSIX
  conversions.
- `%s` that curl did not rewrite subtracts `TimeProvider.LocalTimeZone.BaseUtcOffset`,
  which is what `mktime` does with the `tm_isdst = 0` that `gmtime` sets.

## Consequences

- Once BL-383 lands, `%time{…}` on Linux matches curl 8.21.0 byte for byte for a machine
  whose locale is `C` or `POSIX`. Under another locale curl prints that locale's names and
  `%c %x %X %r` layouts, and Curl prints the C locale's.
- A zone whose standard offset changed in the past gives `%-s` from today's standard offset
  where `mktime` would use the historical one.
- Until BL-383, every platform renders the Windows dialect, as before this decision.

## Alternatives considered

- **Take the pattern from .NET's own date formatting.** .NET has no `strftime`, and its
  custom format strings share none of the flag, width or rejection rules; the fixture's
  1,593 rows would each need a special case.
- **Call the platform's `strftime` through P/Invoke.** It would match any locale, but
  `Curl.Console` publishes native AOT for Windows too, the Windows result would need the
  mingw runtime rather than the UCRT, and the tests could no longer pin Linux output on
  a Windows machine.
- **Follow the machine's locale.** `InvariantGlobalization` leaves no locale data to
  follow, and the C locale is the one that could be measured and pinned.
