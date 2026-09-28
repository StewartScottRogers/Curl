---
id: BL-562
title: Parse --pubkey, --knownhosts, --hostpubmd5, --hostpubsha256 and --compressed-ssh
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-560]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-562 — Parse --pubkey, --knownhosts, --hostpubmd5, --hostpubsha256 and --compressed-ssh

## Goal

The five SSH options parse into `CommandLineOptions` as curl 8.21.0 reads them, including its checks on `--hostpubmd5` (32 hex digits) and `--hostpubsha256` (base64), instead of `is unknown` and exit 2; `--key`, `--key-type` and `--pass` are confirmed to reach the same options object for SSH use.

## Context

- Conformance audit 2026-09-28, row 31. Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs` (`compressed-ssh` accepts `--no-`); texts in `CurlManual.txt`.
- Measure the refusals: `--hostpubmd5 abc`, `--hostpubmd5` with 32 non-hex characters, `--hostpubsha256 !!!`, each with `sftp://127.0.0.1/x`.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -NoServer` (BL-528; the refusals happen before any connection): the cases above, stderr and exit code copied into Notes.
- [x] Every option and `--no-compressed-ssh` is covered by `Curl.Cli.UnitTests` data rows, with the measured refusals byte for byte.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with the local curl 8.21.0 (Windows, Schannel) through
`Record-CurlExchange.ps1 -NoServer`, URL `sftp://127.0.0.1/x` unless stated:

| Arguments | Exit | stderr (try-help = `curl: try 'curl --help' or 'curl --manual' for more information`) |
| --- | --- | --- |
| `--hostpubmd5 abc` | 2 | `curl: option --hostpubmd5: is badly used here` + try-help |
| `--hostpubmd5 zzzz…` (32 × `z`) | 7 | accepted at parse; fails at connect (`Warning: Could not find a known_hosts file`, `curl: (7) Failed to connect …`) |
| `--hostpubmd5 <33 chars>` | 2 | `curl: option --hostpubmd5: is badly used here` + try-help |
| `--hostpubmd5 -x` | 2 | same badly-used lines, no filename warning |
| `-s --hostpubmd5 abc` | 2 | same two lines (`-s` hides nothing) |
| `--hostpubmd5 ''` | 2 | `curl: option --hostpubmd5: blank argument where content is expected` + try-help |
| `--hostpubsha256 !!!` | 7 | accepted at parse (no base64 check); fails at connect |
| `--hostpubsha256 ''` | 2 | `curl: option --hostpubsha256: blank argument where content is expected` + try-help |
| `--pubkey ''` | 2 | `curl: option --pubkey: blank argument where content is expected` + try-help |
| `--pubkey -x` | - | no filename warning |
| `--no-compressed-ssh --pubkey a --knownhosts b` | 2 | `curl: The file 'b' provided to --knownhosts does not exist`, `curl: option --knownhosts: is badly used here`, try-help |
| `--knownhosts ''` | 2 | `curl: The file '' provided to --knownhosts does not exist`, badly-used, try-help |
| `--knownhosts <existing directory>` | 28 | accepted at parse |
| `--knownhosts -x` | 2 | `Warning: The filename argument '-x' looks like a flag.`, then the three lines |
| `-s --knownhosts nope` | 2 | first line hidden: badly-used + try-help only |
| `-sS --knownhosts nope` | 2 | all three lines |
| `-s --cacert nope https://127.0.0.1/x` | 2 | first line hidden as well |

Decisions (no ADR needed; each follows the measurement, the standing "match the platform's
curl" rule):

- `--hostpubmd5` checks only the length (32) at parse, as curl does; hex digits are not
  checked. `--hostpubsha256` is checked for blank only; curl validates neither further here.
- `--knownhosts` shares `--cacert`'s existence check (the helper is renamed
  `SettingExistingFile`). The measurement showed that `-s` without `-S` hides the "The file …
  does not exist" line for `--cacert` too, which the existing code printed anyway, so
  `CommandLineRefusal.FileDoesNotExist` now takes `errorsHidden` and the fix covers all three
  options (a bug in the same refusal, inside this task's `touches`).
- `--key`, `--key-type` and `--pass` stay the rows TLS uses; SSH reads the same
  `PrivateKey`, `PrivateKeyType` and `Passphrase` (pinned by
  `Parse_KeyKeyTypeAndPassWithSftpUrl_RecordTheSshPrivateKey`).
- All five options are per-group (curl keeps them in `OperationConfig`).
- Out of scope, seen while measuring: with `--compressed-ssh`, curl reported
  `curl: Could not find a known_hosts file` / `curl: (2) Failed initialization` at transfer
  time rather than the usual warning; that is the SSH transfer layer's business, not the parser's.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --pubkey, --knownhosts, --hostpubmd5, --hostpubsha256 and --compressed-ssh parse as curl 8.21.0 does, with its measured refusals
