---
id: BL-599
title: Parse --interface and --local-port
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-599 — Parse --interface and --local-port

## Goal

`--interface <name>` (with curl's `if!`, `host!` and `ifhost!` prefixes) and `--local-port <num>[-num]` parse into `CommandLineOptions` with curl 8.21.0's range checks and refusals, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 13 (Major). Binding is BL-600.
- Rows `interface` and `local-port` in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; text in `CurlManual.txt`.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--local-port 0`, `--local-port 70000`, `--local-port 5-3`, `--local-port abc`, `--interface ""`; stderr and exit code copied into Notes.
- [x] Every form and measured refusal is covered by `Curl.Cli.UnitTests` data rows.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with curl 8.21.0 (Windows, Schannel), `Record-CurlExchange.ps1 ... -s -S http://127.0.0.1:47599/`.

`--local-port` refusals, all exit 2 with
`curl: option --local-port: is badly used here` / `curl: try 'curl --help' or 'curl --manual' for more information`:
`70000`, `5-3`, `abc`, `""` (empty: badly used, not blank), `-1`, `-6`, ` -6`, `-`, `+5`, ` 5`, ` 5-6`, `5x`,
`5 6`, `5 `, `5-`, `5- `, `5-0`, `5-abc`, `5-6x`, `5-6 `, `5--6`, `5-+6`, `5- -6`, `5  - 6`, `5-  6`,
`5-70000`, `65535-65536`, `99999999999999999999`.

`--local-port` accepted (exit 0, or a runtime 7 on a privileged port): `0`, `65535`, `0-0`, `0-5`, `5-5`,
`3000-3005`, `5 - 6`, `5 -6`, `5- 6`, `5<TAB>-6`, `5-<TAB>6`, `00005`, `000…005` (27 digits), `5-000…006`.
Rule implemented in `LocalPortRange.TryParse`: digits, then optionally one blank at most, `-`, one blank at
most, digits, end; each port 0-65535, last >= first.

`--interface ""`: exit 2, `curl: option --interface: blank argument where content is expected` + try-help.
Every other value parses; libcurl judges it when curl sets `CURLOPT_INTERFACE`:
- exit 43 `curl: (43) setopt 0x274e got bad argument`: `if!`, `host!`, `ifhost!`, `ifhost!eth0`,
  `ifhost!eth0!`, a plain name of 255+ characters, `if!` + 255+ characters (254 passes, both forms).
- `host!` + 255 characters and `ifhost!lo!` + 255 characters pass setopt (exit 45 at bind);
  `ifhost!` + 255-character interface + `!127.0.0.1` fails at connect with exit 43
  `... A libcurl function was given a bad argument`.
- exit 45 `Failed binding local connection end`: `nosuchif`, `if!nosuch`, ` `, `IF!` (prefixes are
  case-sensitive), `ifhost!!h` (empty interface part is allowed).
- exit 0: `host!127.0.0.1`, `ifhost!lo!127.0.0.1`.

Decisions (defaults taken, no ADR needed - each follows measured curl):
- `CommandLineOptions.Interface` is an `InterfaceBinding` split by prefix, with `IsMalformed` for the
  setopt refusals, so BL-600 binds from parts and fails exit 43 without re-parsing; the parser itself
  accepts every non-empty value, as curl's does.
- `CommandLineOptions.LocalPorts` is a `LocalPortRange` (`First`, `Last`, `Count` = libcurl's
  `CURLOPT_LOCALPORTRANGE`).
- Both are per-group options (curl keeps them in `OperationConfig`), so `--next` resets them.
- The exit-43 and exit-45 behaviour and messages above are handed to BL-600 in its Notes.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --interface (if!/host!/ifhost! split, libcurl's refusals flagged) and --local-port (curl 8.21.0's range syntax and refusals) parse into CommandLineOptions
