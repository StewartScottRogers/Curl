# ADR-0840: On Windows a refused command line echoes argument text in the ANSI code page

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3544, BL-3843, BL-3893)
- Relates to: [ADR-0838](ADR-0838-windows-reads-the-command-line-through-the-ansi-code-page.md), [ADR-0830](ADR-0830-write-out-writes-command-line-text-in-the-ansi-code-page-on-windows.md), CGR-015 of the 2026-10-25 curl gap report in `Documentation/Reviews`

## Context

The Windows reference curl (ucrt64 curl 8.22.0) holds its arguments as ANSI code page bytes (ADR-0838) and
writes them back to standard error as they are. Measured 2026-10-26 with `Record-CurlExchange.ps1 -NoServer`:
`curl --sélent http://127.0.0.1:1/` exits 2 and writes `curl: option --s` E9 `lent: is unknown` (cp1252),
`--sílent` writes ED. Curl wrote those lines as UTF-8 (C3 A9).

A config file's text is different: curl echoes the file's own bytes, which for a UTF-8 file are UTF-8.

## Decision

`CurlCommandRunner` writes a `CommandLineRefusal`'s lines in `CredentialEncoding.ForPlatform(runsOnWindows)` -
the system ANSI code page on Windows (Windows-1252 where the host names none), UTF-8 elsewhere - unless the
refusal echoes a config file line (`CommandLineRefusal.EchoesConfigFileText`), whose lines stay UTF-8.
Every other standard error line is unchanged.

## Consequences

- `curl --sélent` on Windows writes the same bytes as the reference; off Windows nothing changes.
- Tests pass `runsOnWindows: true` and compare against the same encoding, so they pass on every host.
- Other stderr text that echoes argument text (URLs in `curl: (N)` lines, warnings) is still UTF-8; the
  ASCII common case is unaffected.
