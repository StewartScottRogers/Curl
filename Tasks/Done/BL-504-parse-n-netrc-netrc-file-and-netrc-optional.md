---
id: BL-504
title: Parse -n/--netrc, --netrc-file and --netrc-optional
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-504 — Parse -n/--netrc, --netrc-file and --netrc-optional

## Goal

`-n`/`--netrc`, `--netrc-file <path>` and `--netrc-optional` parse into `CommandLineOptions` (whether netrc is required, optional or off, and the file named), with their `--no-` forms as the alias table allows, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 6 (Blocker). Reading the file is BL-503; applying it is BL-505.
- Alias-table rows: `netrc` (`n`, `--no-` accepted), `netrc-file` (no `--no-`), `netrc-optional` (`--no-` accepted) in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`.
- The manual (`CurlManual.txt`) says `--netrc-file` implies `--netrc` and cannot be combined with `--netrc-optional`; how the combination is refused (or not) must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `-n --netrc-optional`, `--netrc-file f --netrc-optional`, `--netrc-file f -n`, `--no-netrc`; stderr and exit code copied into Notes.
- [x] Every spelling and measured combination is covered by `Curl.Cli.UnitTests` data rows.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with the local curl 8.21.0 (x86_64-w64-mingw32, Schannel), through
`Record-CurlExchange.ps1 -Port 45504` against `http://127.0.0.1:45504/`, with no `.netrc` in the
home directory and no file `f` in the working directory:

| Arguments | Exit | Stderr (progress meter left out) |
| --- | --- | --- |
| `-n --netrc-optional` (either order) | 0 | none; the request is sent |
| `--netrc-file f --netrc-optional` (either order) | 2 | `curl: The file 'f' provided to --netrc-file does not exist` / `curl: option --netrc-file: is badly used here` / try-help |
| `--netrc-file f -n` (either order) | 2 | the same three lines |
| `--no-netrc` | 0 | none |
| `--no-netrc-file f` | 2 | `curl: option --no-netrc-file: the given option cannot be reversed with a --no- prefix` / try-help |
| `--netrc-optional --no-netrc-optional -n` | 26 | `curl: (26) .netrc error: no such file` |

Also measured directly: `--no-netrc-optional` and `-n --no-netrc --netrc-optional` are accepted;
`-n -n` exits 26 like `-n`; `-s --netrc-file nope` drops the first line (as `--cacert` does);
`--netrc-file=nope` names `--netrc-file=nope` in the second line; `--netrc-file ''` is refused as a
missing file, not as blank; `--netrc-file -s` warns `Warning: The filename argument '-s' looks like a
flag.` first; `--netrc-optional=x` is accepted; with an existing `f`, `--netrc-file f --netrc-optional`,
`--netrc-file f -n`, `--netrc-file f --no-netrc` and `--netrc-file .` (a directory) all pass parsing.

What this means, and what the parser now does:
- The manual calls `--netrc`, `--netrc-file` and `--netrc-optional` mutually exclusive, but curl 8.21.0
  refuses no combination: every refusal above is the missing-file check. `--netrc-optional` wins over
  `-n` in either order (exit 0 with no `.netrc`); after `--no-netrc-optional`, `-n` is required again
  (exit 26). So `CommandLineOptions.NetrcUse` is `Optional` when `--netrc-optional` is in effect, else
  `Required` when `-n` or `--netrc-file` is, else `Ignored`.
- `--netrc-file` checks existence when read, exactly as `--cacert`/`--knownhosts` do, so it reuses
  `SettingExistingFile`. `--no-netrc` leaves a named file in place.
- The three options are per-group (curl keeps them per operation), so they joined
  `PerGroupOptionLongNames` in `CommandLineNextGroupTests`.
- No ADR: every behaviour above is measured, not chosen.

Delivered: `NetrcUse` enum; `NetrcRequested`, `NetrcOptionalRequested`, `NetrcFile` and `NetrcUse` on
`CommandLineOptions`; three rows in `CommandLineOptionTable`; `CommandLineNetrcOptionTests` (32 cases);
README updated. Build clean with `-warnaserror`; fast tests all green (Curl.Cli.UnitTests 2475 passed);
`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members,
worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -n/--netrc, --netrc-file and --netrc-optional parse into CommandLineOptions.NetrcUse and NetrcFile as curl 8.21.0 does
