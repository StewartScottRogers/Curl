---
id: BL-501
title: Parse -1/--tlsv1, --tlsv1.0, --tlsv1.1, --tls-max and --proxy-tlsv1
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-501 — Parse -1/--tlsv1, --tlsv1.0, --tlsv1.1, --tls-max and --proxy-tlsv1

## Goal

`-1`/`--tlsv1`, `--tlsv1.0`, `--tlsv1.1` set the minimum TLS version as `--tlsv1.2` and `--tlsv1.3` already do, `--tls-max <VERSION>` sets a maximum (`1.0`, `1.1`, `1.2`, `1.3`, `default`), and `--proxy-tlsv1` sets the HTTPS proxy's minimum, with curl 8.21.0's refusal for a bad `--tls-max` value.

## Context

- Conformance audit 2026-09-28, row 5 (Blocker).
- `--tlsv1.2`/`--tlsv1.3` are rows in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` setting `MinimumTlsVersion` (`SslProtocols`); follow them. `SslProtocols.Tls` and `Tls11` are obsolete in .NET (SYSLIB0039) and warnings are errors, so choose a representation that does not trip the analyzer (for instance a Curl-owned enum, as `Curl.Networking.UnitLibrary/TlsMinimumVersion.cs` suggests).
- Using the values is BL-502.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--tls-max 1.4`, `--tls-max abc` and `--tls-max` with no value; stderr and exit code copied into Notes.
- [x] Every spelling (including `-1` in a bundle) is covered by `Curl.Cli.UnitTests` data rows; the measured refusals are pinned byte for byte.
- [x] `dotnet build Curl.slnx -warnaserror` is clean (no SYSLIB0039 suppression outside one documented place), the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with `Record-CurlExchange.ps1 -NoServer` (curl 8.21.0, Schannel, x86_64-w64-mingw32), `http://127.0.0.1:1/` as the URL:

| Arguments | Exit | stderr |
| --- | ---: | --- |
| `--tls-max 1.4` | 2 | `curl: option --tls-max: is badly used here` + try-help line |
| `--tls-max abc` | 2 | same |
| `--tls-max DEFAULT`, `--tls-max 1`, `--tls-max "1.2 "`, `--tls-max ""` | 2 | same (exact, case-sensitive match) |
| `--tls-max` (no value) | 2 | `curl: option --tls-max: requires parameter` + try-help line |
| `--tls-max default`/`1.0`/`1.1`/`1.2`/`1.3` | 7 or 28 | accepted; the connect fails |
| `--no-tlsv1`, `--no-tlsv1.0`, `--no-tlsv1.1`, `--no-tls-max`, `--no-proxy-tlsv1` | 2 | `curl: option <as typed>: the given option cannot be reversed with a --no- prefix` + try-help line |
| `-s1S` | 7 | accepted; `-1` bundles |

The try-help line is `curl: try 'curl --help' or 'curl --manual' for more information`.

Decisions (the sensible defaults, rule 1; no ADR because nothing outside the parser changes):
- `MinimumTlsVersion` stays `SslProtocols?`, so `Curl.Console` (touched by BL-515 in Doing) needs no change here. Today its mapping sends TLS 1.0/1.1 minimums to `TlsMinimumVersion.SystemDefault`; BL-502 maps them.
- TLS 1.0 and 1.1 are the public constants `ObsoleteTlsProtocols.Tls10`/`Tls11`, the one `#pragma warning disable SYSLIB0039` in the library, so BL-502 can reuse them instead of suppressing again.
- New `MaximumTlsVersion` (`SslProtocols?`, `null` for not given or `default`) and `ProxyMinimumTlsVersion` (`SslProtocols?`). curl applies `--tls-max` to the origin and the proxy alike; that is recorded in the XML doc for BL-502.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -1/--tlsv1, --tlsv1.0, --tlsv1.1, --tls-max and --proxy-tlsv1 parse, with curl 8.21.0's refusals pinned
