---
id: BL-522
title: Parse --proto, --proto-redir and --proto-default into scheme sets
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-522 — Parse --proto, --proto-redir and --proto-default into scheme sets

## Goal

`--proto <protocols>` and `--proto-redir <protocols>` are read with curl 8.21.0's syntax (comma-separated names, `+` add, `-` remove, `=` set, `all`, applied left to right) into the set of allowed schemes, and `--proto-default <protocol>` into one scheme, with curl's warnings and refusals for unknown names.

## Context

- Conformance audit 2026-09-28, row 10 (Blocker). Enforcing them is BL-523 and BL-524.
- Alias-table rows `proto`, `proto-redir`, `proto-default` (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`). The syntax is in `CurlManual.txt` (`--proto`) and https://curl.se/docs/manpage.html#--proto (8.21.0 reference).
- The scheme names are those curl 8.21.0 knows, including ones Curl does not implement; the unknown-name warning text and whether an unknown `--proto-default` is refused must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--proto =http,https`, `--proto -all,+http`, `--proto http,bogus`, `--proto ""`, `--proto-default bogus`, each with a loopback `http://` URL; stderr and exit code copied into Notes.
- [x] `Curl.Cli.UnitTests` data rows cover each syntax form, order of application, `all`, and the measured warning and refusal texts byte for byte.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured with `Record-CurlExchange.ps1` against the Windows curl 8.21.0 (Schannel), 2026-09-28, each
with `-s -S http://127.0.0.1:48522/` after the option (stderr lines end CRLF):

| Arguments | Exit | stderr |
| --- | ---: | --- |
| `--proto =http,https` | 0 | (none) |
| `--proto -all,+http` | 0 | (none) |
| `--proto http,bogus` | 0 | `Warning: unrecognized protocol 'bogus'` |
| `--proto ""` | 0 | (none) |
| `--proto-default bogus` | 1 | `curl: option --proto-default: a specified protocol is unsupported by libcurl` + try-help line |
| `--proto-default ""` | 2 | `curl: option --proto-default: blank argument where content is expected` + try-help line |
| `--proto -all` / `=http,-http` | 2 | `curl: option --proto: is badly used here` + try-help line |
| `--proto =bogus` / `=` | 2 | the warning (`'bogus'` / `''`), then badly used + try-help line |
| `--proto ++http` | 0 | `Warning: unrecognized protocol '+http'` (one modifier only) |
| `--proto ' http , bogus'` | 0 | warns `' http '` and `' bogus'` (no trimming) |
| `--proto ,` | 0 | (none: empty items skipped); `+` and `=,http` warn `''` |
| `--proto` 32+ char name | 0 | name cut to its first 31 characters, after the modifier |
| `--proto ipfs,ws,wss,rtmp,scp` / `smb` | 0 | warns `ipfs`, `rtmp` / `smb`: unknown to this build |
| `--proto-default all` / `smb` / `ipfs` | 1 | unsupported by libcurl (as `bogus`) |
| `--proto -http --proto +ftp` | 0 | http transfers: each `--proto` restarts from every scheme |
| `-s --proto http,bogus` | 0 | (none: `-s` first hides the warning); `-s` does not hide the refusals |

`HTTP`, `=HtTp`, `ALL`, `-ALL,+http` and `--proto-default HTTPS` are taken silently (case-insensitive).

Decisions (Decided by Claude under Stewart's delegation; ADR filed as BL-746, because BL-515 holds
`Documentation/Planning/Decisions` in its `touches` right now):
- Known schemes: one list on every platform, the Windows curl 8.21.0 `Protocols:` line less `ipfs`
  and `ipns` (measured unknown to its libcurl). Keeps tests platform-neutral; the OpenSSL builds may
  know `smb`/`rtmp` too.
- Stored as lowercase `IReadOnlySet<string>` (`AllowedProtocols`, `AllowedRedirectProtocols`) and
  `DefaultProtocol` string; `null` = not given. Enforcement is BL-523 and BL-524.

`Record-CurlExchange.ps1` added to `touches` (no task in Doing names it): `-CurlArgs` now accepts an
empty element (`[AllowEmptyString()]`) so `--proto ""` and `--proto-default ""` could be measured.

`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members
(worst CRAP 10). One coverage run failed its `dotnet test` once with no failed test reported; the rerun
and a manual coverage run passed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --proto, --proto-redir and --proto-default are read into scheme sets with curl 8.21.0's syntax, warnings and refusals
