---
id: BL-457
title: Parse -P/--ftp-port, --disable-eprt, --ssl, --ssl-reqd and --ftp-ssl* into CommandLineOptions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-459]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-457 — Parse -P/--ftp-port, --disable-eprt, --ssl, --ssl-reqd and --ftp-ssl* into CommandLineOptions

## Goal

`-P`/`--ftp-port <address>`, `--disable-eprt` (and `--eprt`), `--ssl`, `--ftp-ssl`, `--ssl-reqd`, `--ftp-ssl-reqd` and `--ftp-ssl-control` parse into `CommandLineOptions` properties matching ADR-0102's transfer options, as curl 8.21.0 parses them.

## Context

- ADR-0102, "Contract additions", item 3. `TransportSecurityLevel` comes from BL-459.
- Today these names exist only in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; `CommandLineOptionTable.cs` has no entry. `--disable-epsv` and `--ftp-method` (BL-436) show how FTP options were added.
- curl: `--ftp-ssl` is the old name of `--ssl`, `--ftp-ssl-reqd` of `--ssl-reqd`; `--ftp-ssl-control` requires TLS for the control connection only; `--no-ssl` and `--no-ssl-reqd` turn them off. Measure how curl 8.21.0 combines them (for example `--ssl-reqd --no-ssl`) before pinning, and record the answers under Notes.

## Acceptance criteria

- [x] `CommandLineOptions` carries `FtpPort`, `FtpUseEprt`, `SslLevel` and `FtpSslControlOnly`, with the defaults of ADR-0102; each option and its `--no-` form is pinned by a named test in `Curl.Cli.UnitTests`.
- [x] `-P` without a value fails as curl 8.21.0 fails (measured exit code and message pinned in a named test).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for `Curl.Cli.UnitLibrary`.

## Notes

Filed by BL-437 under ADR-0102. BL-458 depends on this task.

Measured 2026-09-27 against the local curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Ftp`
(its server refuses `AUTH`, `EPRT` and `PORT` with 502) and direct runs:

- `--ssl`/`--ftp-ssl`: `AUTH SSL`, `AUTH TLS`, then plaintext login, exit 0. Each occurrence warns
  `Warning: --ssl is an insecure option, consider --ssl-reqd instead` (the long name as typed,
  `--ftp-ssl` for that spelling, no value even for `--ssl=x`); `-s` before it drops the warning,
  `-s` after keeps it; `--no-ssl` never warns.
- `--ssl-reqd`, `--ftp-ssl-reqd`, `--ftp-ssl-control` (alone) each: `AUTH SSL`, `AUTH TLS`, then
  `curl: (64) Requested SSL level failed`.
- The three flags are independent: `--ssl-reqd --no-ssl` still fails 64; `--ssl --no-ssl-reqd` still
  tries; `--ssl --ftp-ssl-control` fails 64; `--ssl --no-ssl`, `--ssl-reqd --no-ssl-reqd` and
  `--ftp-ssl-control --no-ftp-ssl-control` send no `AUTH`.
- `-P -` sends `EPRT |1|127.0.0.1|<port>|` then `PORT`; `--disable-eprt` and `--no-eprt` send `PORT`
  only; `--disable-eprt --no-disable-eprt` and `--no-eprt --eprt` send `EPRT` again.
- `-P`/`--ftp-port` as last argument: `curl: option -P: requires parameter` + try-help, exit 2;
  blank value: `curl: option -P: blank argument where content is expected`, exit 2 (so a `Text`
  row); `--no-ftp-port` is refused as not reversible.
- `-P - --ftp-pasv` goes passive again (`EPSV`): `--ftp-pasv` has no row yet, filed as BL-463.

Decisions (by Claude under Stewart's delegation, within ADR-0102's contract, no new ADR since
`Documentation/Planning/Decisions` is in BL-437's `touches`):

- `SslLevel` is computed from three independent flags as curl keeps them: `Required` when
  `--ssl-reqd`/`--ftp-ssl-reqd` or `--ftp-ssl-control` is on, else `Try` when `--ssl`/`--ftp-ssl` is,
  else `None`. `--ftp-ssl-control` implies `Required` because alone it fails 64 like `--ssl-reqd`
  (libcurl `CURLUSESSL_CONTROL`); `FtpSslControlOnly` then tells the handler to send `PROT C`. curl's
  tool sets `CURLOPT_USE_SSL` for ssl, then ssl-reqd, then ssl-control, so control-only outranks
  `--ssl-reqd` whatever the command-line order (from curl's `config2setopts`; the data-connection
  half is not measurable until BL-437's TLS recorder lands).
- `-P` is stored verbatim; the handler (BL-437) parses the address.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -P/--ftp-port, --disable-eprt/--eprt, --ssl/--ftp-ssl, --ssl-reqd/--ftp-ssl-reqd and --ftp-ssl-control parse into CommandLineOptions as curl 8.21.0 does
