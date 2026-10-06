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
completed: 2026-09-28
---
# BL-605 — Parse the twelve HTTPS-proxy TLS options

## Goal

`--proxy-cert`, `--proxy-key`, `--proxy-cert-type`, `--proxy-key-type`, `--proxy-pass`, `--proxy-ciphers`, `--proxy-tls13-ciphers`, `--proxy-crlfile`, `--proxy-pinnedpubkey`, `--proxy-ca-native`, `--proxy-ssl-auto-client-cert` and `--proxy-ssl-allow-beast` parse into the proxy's TLS settings on `CommandLineOptions`, as their origin counterparts do, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 15 (Major). Applying them: BL-606 and BL-611.
- The origin options (`--cert`, `--key`, `--cert-type`, `--key-type`, `--pass`, `--ciphers`, `--tls13-ciphers`) are already parsed; follow them (`Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`, `ClientCertificateArgument` in Networking for the `cert:password` split). `--proxy-cacert` and `--proxy-insecure` are already rows (ADR-0095).
- `--proxy-cert` splits `file:password` as `--cert` does, including the Windows store-path form (ADR-0066).

## Acceptance criteria

- [x] Every option (and the `--no-` forms the alias table allows) is covered by `Curl.Cli.UnitTests`, including a `--proxy-cert` with a password and an escaped colon.
- [x] The origin options' values are unaffected by the proxy ones and vice versa, with tests.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Each option is a row beside its origin counterpart in `CommandLineOptionTable.cs`, using the same helper: `--proxy-cert`/`--proxy-key` are `FileName` rows (warn on a flag-like value), `--proxy-crlfile` checks the file exists as `--crlfile` does, the rest are `Text` rows, and the three switches are `NegatableFlag` rows. Values land on new `Proxy*` properties of `CommandLineOptions`; all are per-group, as their origin counterparts are.
- `--proxy-cert` is kept verbatim; the `cert[:password]` split (escaped colons, Windows store path, ADR-0066) stays where the origin value is split, at application time (BL-606). The tests pin that `proxy\:name.pem:se\:cret` and a store path arrive unchanged.
- Measured against curl 8.21.0 (Schannel), 2026-09-28: `--proxy-cert -zz` and `--proxy-key -zz` warn "looks like a flag"; the other value options do not; `--proxy-crlfile nosuchfile.x` exits 2 with "The file ... provided to --proxy-crlfile does not exist" / "is badly used here"; `--no-proxy-ca-native`, `--no-proxy-ssl-allow-beast`, `--no-proxy-ssl-auto-client-cert` are accepted; `--no-` on every value option exits 2 "cannot be reversed".
- Also measured, for BL-606: on Schannel, `--proxy-tls13-ciphers x` prints "Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel" (silenced by `-s`), as `--tls13-ciphers` does for the origin. That is transfer-time behaviour in Networking/Console, outside this task's touches; BL-606 already covers applying the cipher lists as measured.
- Tests: `Curl.Cli.UnitTests/CommandLineProxyTlsOptionTests.cs` (new), plus the per-group classification list and the option-table rows. Curl.Cli.UnitTests 2655 passed, 13 skipped; Measure-CodeQuality reports 100% line and branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The twelve HTTPS-proxy TLS options parse into their own Proxy* properties on CommandLineOptions, as their origin counterparts do
