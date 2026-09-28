---
id: BL-506
title: Parse --unix-socket and --abstract-unix-socket
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-506 — Parse --unix-socket and --abstract-unix-socket

## Goal

`--unix-socket <path>` and `--abstract-unix-socket <path>` parse into `CommandLineOptions` (the path, and whether it is abstract; the later option wins), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 7 (Blocker). Dialling is BL-507.
- Alias-table rows `unix-socket` and `abstract-unix-socket` (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`), no `--no-` form.
- Whether the Windows reference build accepts `--abstract-unix-socket` at parse time or refuses it later must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--abstract-unix-socket x http://h/` and `--unix-socket "" http://h/` on the reference build; stderr and exit code copied into Notes.
- [x] Both options and their interplay are covered by `Curl.Cli.UnitTests` data rows; a measured parse-time refusal is pinned.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with curl 8.21.0 (x86_64-w64-mingw32, Schannel) through `Record-CurlExchange.ps1 -Port 45506`, URL `http://127.0.0.1:45506/`. No request bytes reached the server in any case.

| Arguments before the URL | Exit | stderr |
| --- | ---: | --- |
| `--abstract-unix-socket x` | 7 | `curl: (7) Failed to connect to 127.0.0.1:45506 over unix://x after 0 ms: Could not connect to server` |
| `--unix-socket ""` | 2 | `curl: option --unix-socket: blank argument where content is expected` + try-help line |
| `--abstract-unix-socket ""` | 2 | `curl: option --abstract-unix-socket: blank argument where content is expected` + try-help line |
| `--unix-socket nosuch.sock` | 7 | `curl: (7) Failed to connect to 127.0.0.1:45506 over unix://nosuch.sock after 0 ms: Could not connect to server` |
| `--unix-socket nosuch.sock --abstract-unix-socket ""` | 2 | blank refusal naming `--abstract-unix-socket` |
| `--abstract-unix-socket x --unix-socket ""` | 2 | blank refusal naming `--unix-socket` |
| `--unix-socket nosuch.sock --unix-socket ""` | 2 | blank refusal naming `--unix-socket` |
| `--no-unix-socket x` | 2 | `curl: option --no-unix-socket: the given option cannot be reversed with a --no- prefix` + try-help |
| `--no-abstract-unix-socket x` | 2 | `curl: option --no-abstract-unix-socket: the given option cannot be reversed with a --no- prefix` + try-help |
| `--unix-socket -s` | 7 | `Warning: The filename argument '-s' looks like a flag.` then the exit-7 connect line for `unix://-s` |

- The Windows reference build accepts `--abstract-unix-socket` at parse time; the refusal comes only when dialling (exit 7), which is BL-507's. So no parse-time refusal is pinned for it; the measured parse-time refusals (blank, `--no-`) are.
- Both are `CommandLineOption.FileName` rows: that gives the blank refusal and the looks-like-a-flag warning measured above. They share one slot (`SetUnixSocket`), so the later option wins path and kind, matching curl's single `unix_socket_path` + `abstract_unix_socket` pair in `OperationConfig`; for the same reason both are per-group options in `CommandLineNextGroupTests`.
- No ADR: nothing was chosen that the measurement did not decide.
- Tests: `Curl.Cli.UnitTests/CommandLineUnixSocketOptionTests.cs` (18 cases). Cli tests 2493 passed; Measure-CodeQuality: Curl.Cli.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --unix-socket and --abstract-unix-socket parse into UnixSocketPath/UnixSocketIsAbstract, last wins, blank and --no- refused as curl 8.21.0 does
