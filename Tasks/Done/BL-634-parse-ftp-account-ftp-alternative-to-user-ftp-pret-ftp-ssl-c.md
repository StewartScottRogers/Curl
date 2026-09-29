---
id: BL-634
title: Parse --ftp-account, --ftp-alternative-to-user, --ftp-pret, --ftp-ssl-ccc and --ftp-ssl-ccc-mode
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-634 — Parse --ftp-account, --ftp-alternative-to-user, --ftp-pret, --ftp-ssl-ccc and --ftp-ssl-ccc-mode

## Goal

The five FTP options parse into `CommandLineOptions` with curl 8.21.0's value checks (`--ftp-ssl-ccc-mode active|passive`), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 25 (Major). Behaviour: BL-635 (ACCT, alternative user, PRET) and BL-636 (CCC).
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; the other FTP options (`--ftp-method`, `--ftp-pasv`, `--disable-epsv`) are rows in `CommandLineOptionTable.cs` to follow.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp`: `--ftp-ssl-ccc-mode bogus`; stderr and exit code copied into Notes.
- [x] Every option and `--no-` form is covered by `Curl.Cli.UnitTests`, with the measured refusal.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with `Record-CurlExchange.ps1 -Ftp` against curl 8.21.0 (Schannel, Windows), URL `ftp://127.0.0.1:<port>/f.txt`:

- `--ftp-ssl-ccc-mode bogus`: exit 0, stderr `Warning: unrecognized ftp CCC method 'bogus', using default`, then the transfer. A warning, not a refusal.
- `--ftp-ssl-ccc-mode ''`: exit 0, the same warning with `''`. `ACTIVE` and `passive`: no warning (case-folded).
- A value long enough to wrap: `Warning: unrecognized ftp CCC method ` / `Warning: 'a-very-long-...-sevent` / `Warning: y-nine-columns', using default` (the 79-column wrap `WrappedMessage` already does).
- `-s --ftp-ssl-ccc-mode bogus`: no warning.
- `--no-ftp-account x`, `--no-ftp-alternative-to-user x`, `--no-ftp-ssl-ccc-mode x`, `--no-ftp-ssl-ccc-mode=x`: exit 2, `curl: option <arg>: the given option cannot be reversed with a --no- prefix` and the try-help line.
- `--ftp-account ''`, `--ftp-alternative-to-user ''`: exit 2, `blank argument where content is expected`.
- `--no-ftp-pret`, `--no-ftp-ssl-ccc`: accepted, exit 0. `--ftp-pret=x` turns PRET on (the loopback refused PRET, exit 84).

Model (default taken; no ADR, since it copies curl's `tool_getparam.c` rather than choosing): curl keeps the CCC flag and the mode apart. `--ftp-ssl-ccc-mode` turns the flag on and sets the mode; `--ftp-ssl-ccc` and `--no-ftp-ssl-ccc` toggle only the flag; the mode defaults to passive. `CommandLineOptions.FtpClearCommandChannel` (new enum `FtpClearCommandChannel`: `Off`, `Passive`, `Active`) is the one effective answer, so `--ftp-ssl-ccc-mode active --no-ftp-ssl-ccc --ftp-ssl-ccc` is `Active`. The enum lives in `Curl.Cli`, inside this task's `touches`; BL-636 maps it onto whatever the FTP handler takes. The other three are `FtpAccount`, `FtpAlternativeToUser` (`Text` rows) and `FtpSendPret` (`NegatableFlag`). All five are per-group options.

Gates: `dotnet build Curl.slnx -warnaserror` clean; every fast test project green (Curl.Cli.UnitTests 2712 passed, 13 platform skips); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --ftp-account, --ftp-alternative-to-user, --ftp-pret, --ftp-ssl-ccc and --ftp-ssl-ccc-mode parse as curl 8.21.0 does
