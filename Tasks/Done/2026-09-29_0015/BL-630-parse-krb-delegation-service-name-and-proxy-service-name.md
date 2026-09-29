---
id: BL-630
title: Parse --krb, --delegation, --service-name and --proxy-service-name
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-630 — Parse --krb, --delegation, --service-name and --proxy-service-name

## Goal

`--krb <level>`, `--delegation <none|policy|always>`, `--service-name <name>` and `--proxy-service-name <name>` parse into `CommandLineOptions` with curl 8.21.0's value checks on every platform, `--krb` (FTP Kerberos) included; FTP Kerberos itself is built by BL-693.

## Context

- Conformance audit 2026-09-28, row 23 (Major). `--sasl-authzid`, `--sasl-ir` and `--login-options` are BL-535. Applying the names is BL-631.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; texts in `CurlManual.txt`. FTP Kerberos (`--krb`) is missing from some builds, but curl's GSS-API builds have it, so Curl accepts it everywhere (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28: if any official curl build supports a feature, Curl supports it on every platform); a reference build's refusal is recorded in Notes for information only. Only malformed values are refused, with curl's text.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp`: `--krb private ftp://...`, `--krb bogus`, `--delegation bogus`; stderr and exit code copied into Notes, on Windows and on Linux or macOS.
- [x] `Curl.Cli.UnitTests` cover every option on every platform, and each malformed-value refusal with the text measured on a build that has the option.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28. Windows: curl 8.21.0 (Schannel) through `Record-CurlExchange.ps1 -Ftp -Port 52630`.
Linux: curl 8.18.0 (OpenSSL, mit-krb5 1.22.1, GSS-API) under WSL Ubuntu against `ftp://127.0.0.1:1/x`
(the loopback server is not reachable from WSL, and these messages come from option parsing, before any
connection). Both builds answer alike:

| Arguments | stderr (first line) | Exit (Windows / Linux) |
| --- | --- | --- |
| `--krb private ftp://...` | `Warning: --krb is deprecated and has no function anymore`, then the transfer | 0 / 7 (port 1) |
| `--krb bogus`, `--krb CLEAR`, `--krb ''` | the same warning, then the transfer | 0 / 7 |
| `--delegation bogus` | `Warning: unrecognized delegation method 'bogus', using none`, then the transfer | 0 / 7 |
| `--delegation ''` | `Warning: unrecognized delegation method '', using none` | 0 |
| `--delegation policy`, `Policy`, `ALWAYS`, `none` | nothing | 0 / 7 |
| `--service-name ''` | `curl: option --service-name: blank argument where content is expected` + try-help | 2 / 2 |
| `--proxy-service-name ''` | `curl: option --proxy-service-name: blank argument where content is expected` + try-help | 2 / 2 |
| `--krb` (last) | `curl: option --krb: requires parameter` + try-help | 2 |
| `--no-krb`, `--no-delegation`, `--no-service-name`, `--no-proxy-service-name` | `... cannot be reversed with a --no- prefix` + try-help | 2 |

Also (Windows): a long `--delegation` value wraps its warning at 79 columns as `--ftp-method`'s does; `-s`
before `--delegation bogus` drops the warning, after it does not.

Decision: no new ADR. The Goal's "`--krb` (FTP Kerberos) included; FTP Kerberos itself is built by BL-693"
was overtaken by ADR-0142 (BL-525), which measured the same thing: both platform curls, the GSS-API Linux
build included, treat `--krb` as deprecated with no function, and BL-693 is Deferred. So `--krb` is a
`NoFunctionValue` row beside `--krb4`, and "only malformed values are refused" reduces to: no `--krb` value is
refused, `--delegation` warns rather than refuses, and the two service names refuse only an empty value.
`--delegation` lands in a new Cli-local `GssApiDelegation` enum (None, Policy, Always) rather than
`Curl.Kerberos.UnitLibrary`'s `KerberosDelegation`, since Cli does not reference Kerberos; BL-631 maps it where
the names are applied. All four are per-group options, as curl keeps them in its per-operation config.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --delegation, --service-name and --proxy-service-name parse as curl 8.21.0 does; --krb warns it has no function (ADR-0142)
