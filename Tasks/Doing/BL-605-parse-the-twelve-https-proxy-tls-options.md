---
id: BL-605
title: Parse the twelve HTTPS-proxy TLS options
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-605 — Parse the twelve HTTPS-proxy TLS options

## Goal

`--proxy-cert`, `--proxy-key`, `--proxy-cert-type`, `--proxy-key-type`, `--proxy-pass`, `--proxy-ciphers`, `--proxy-tls13-ciphers`, `--proxy-crlfile`, `--proxy-pinnedpubkey`, `--proxy-ca-native`, `--proxy-ssl-auto-client-cert` and `--proxy-ssl-allow-beast` parse into the proxy's TLS settings on `CommandLineOptions`, as their origin counterparts do, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 15 (Major). Applying them: BL-606 and BL-611.
- The origin options (`--cert`, `--key`, `--cert-type`, `--key-type`, `--pass`, `--ciphers`, `--tls13-ciphers`) are already parsed; follow them (`Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`, `ClientCertificateArgument` in Networking for the `cert:password` split). `--proxy-cacert` and `--proxy-insecure` are already rows (ADR-0095).
- `--proxy-cert` splits `file:password` as `--cert` does, including the Windows store-path form (ADR-0066).

## Acceptance criteria

- [ ] Every option (and the `--no-` forms the alias table allows) is covered by `Curl.Cli.UnitTests`, including a `--proxy-cert` with a password and an escaped colon.
- [ ] The origin options' values are unaffected by the proxy ones and vice versa, with tests.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
