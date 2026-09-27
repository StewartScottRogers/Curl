# ADR-0048 — `-o` names are sanitized on Windows by a separate step, only when globbing

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (task BL-283, recorded by BL-314, 2026-09-27).

## Context

ADR-0032 left one step of curl's `-o` name handling out of
`UrlGlobMatch.SubstituteGlobValues`: in curl 8.21.0's `tool_urlglob.c`, `glob_match_url`
substitutes each `#N` and then, only on Windows and MS-DOS builds
(`#if defined(_WIN32) || defined(MSDOS)`), runs `sanitize_file_name` on the result with
`SANITIZE_ALLOW_PATH | SANITIZE_ALLOW_RESERVED`. BL-283 had to decide where that step lives,
whether it runs off Windows, and what it replaces.

Measured on 2026-09-26 with the Windows reference build (`/mingw64/bin/curl`, curl 8.21.0
x86_64-w64-mingw32, Schannel; see ADR-0018) as
`curl -s -w "%{filename_effective}|%{exitcode}\n" -o '<name>' '<url>'` over `file:///n/...`,
every transfer failing with exit 37:

| `-o` name | URL | Name curl writes to |
| --- | --- | --- |
| `o_#1` | `file:///n/{a?b,c*d,e:f,g"h,i<j,k>l,m\|n,q/r}` | `o_a_b o_c_d o_e:f o_g_h o_i_j o_k_l o_m_n o_q/r` |
| `o_#1` | `file:///n/{CON,a.,b%20,a%3Fb}` | `o_CON o_a. o_b%20 o_a%3Fb` |
| `o?_#1` | `file:///n/{a,b}` | `o__a o__b` |
| `o?x` | `file:///n/a` | `o_x` |
| `o?x` with `-g` | `file:///n/a` | `o?x` |
| `\\?\C:\tmp\a?b` | `file:///n/a` | `\\?\C:\tmp\a_b` |
| `\\srv\a?b` | `file:///n/a` | `\\srv\a_b` |
| `a\\b\c` | `file:///n/a` | `a\\b\c` |

So: `? * " < > |` and the control characters U+0001 to U+001F (tab included) become `_`;
`/`, `\`, `:`, reserved device names, trailing dots, percent text, U+007F and `é` stay; the
whole name is sanitized, not only the substituted values; it is sanitized even when the URL
holds no glob; and under `-g` it is not touched, because curl skips `glob_match_url`
entirely. Names of 259, 260, 300, 32767 and 40000 characters (the long ones through `-K`)
all came back unchanged: 8.21.0 sets no length limit on this path.

## Decision

1. The step is its own type, `internal static WindowsOutputFileNameSanitizer` in
   `Curl.Core.Globbing`, whose `Sanitize` replaces the characters above with `_` and keeps a
   leading `\\?\` long-path prefix whole. `SubstituteGlobValues` stays the pure `#N` step.
2. `UrlGlobMatch.ResolveOutputFileName(outputFileName, sanitizesForWindows)` is the one entry
   point for the `-o` name. When the `UrlGlob` came from `UrlGlob.Unglobbed` (`-g`) it
   returns the name as written. Otherwise it substitutes `#N` and, only when
   `sanitizesForWindows` is `true`, sanitizes the whole substituted name.
3. `UrlGlob` records whether it came from `TryParse` (globbing on) or `Unglobbed` (globbing
   off) and passes that to each `UrlGlobMatch` it expands, so the `-g` case above holds.
4. The platform is a parameter, not `OperatingSystem.IsWindows()` read inside: the caller
   passes `OperatingSystem.IsWindows()`, so sanitizing runs only on Windows, as curl's does.

## Consequences

- On Windows a glob value holding `?`, `*`, `"`, `<`, `>` or `|` gives the file name real curl
  writes; off Windows the name is only substituted, as curl's Linux and macOS builds leave it.
- Both branches are tested on any operating system (`UrlGlobTests` in `Curl.Core.UnitTests`
  pins every measured case above), because no test depends on the host.
- A caller that forgets to pass `OperatingSystem.IsWindows()` gets no sanitizing on Windows;
  nothing in `Curl.Core` can catch that.
- As of 2026-09-27 nothing in `Curl.Console` calls `ResolveOutputFileName` or references
  `UrlGlob` at all: the wiring is BL-240. The line in ADR-0032's Consequences that has
  `Curl.Console` apply `SubstituteGlobValues` to the `-o` name is refined by this ADR: the
  caller uses `ResolveOutputFileName` instead.
- A name Windows still cannot create (a reserved device name, a trailing dot, an over-long
  path) is passed on unchanged, as curl passes it, and fails where curl's fails.

## Alternatives considered

- **Sanitize inside `SubstituteGlobValues`.** It would hide a platform-specific step behind a
  name that says substitution, and the substitution could no longer be tested alone.
- **Sanitize only the substituted values.** The measurement shows curl sanitizes the whole
  name: `o?_#1` gives `o__a`, and `o?x` with no glob gives `o_x`.
- **A length limit.** None was measured: a 40000-character name came back unchanged.
- **Read `OperatingSystem.IsWindows()` inside `ResolveOutputFileName`.** Only the host's
  branch could then be tested on each machine, leaving the other untested wherever the
  suite runs; a parameter tests both everywhere and costs the caller one argument.
