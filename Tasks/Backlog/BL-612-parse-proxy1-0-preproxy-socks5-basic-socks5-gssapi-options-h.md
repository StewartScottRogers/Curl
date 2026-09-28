---
id: BL-612
title: Parse --proxy1.0, --preproxy, --socks5-basic, --socks5-gssapi options, --haproxy-protocol, --haproxy-clientip and --suppress-connect-headers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-612 — Parse --proxy1.0, --preproxy, --socks5-basic, --socks5-gssapi options, --haproxy-protocol, --haproxy-clientip and --suppress-connect-headers

## Goal

`--proxy1.0 <host>`, `--preproxy <url>`, `--socks5-basic`, `--socks5-gssapi`, `--socks5-gssapi-service <name>`, `--socks5-gssapi-nec`, `--haproxy-protocol`, `--haproxy-clientip <ip>` and `--suppress-connect-headers` parse into `CommandLineOptions` as curl 8.21.0 reads them, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Behaviour: BL-613 to BL-616.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`. `--proxy1.0` sets the proxy as `-x` does (`CommandLineProxy.cs`) and marks it HTTP/1.0; `--preproxy` takes a SOCKS URL (`Curl.Core.UnitLibrary/ProxyUrlParser.cs` reads proxy URLs).

## Acceptance criteria

- [ ] Every option and `--no-` form the alias table allows is covered by `Curl.Cli.UnitTests`; `--proxy1.0` and `-x` interplay (last wins or not) is measured with `Record-CurlExchange.ps1` and pinned.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
