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
completed: 2026-09-28
---
# BL-612 — Parse --proxy1.0, --preproxy, --socks5-basic, --socks5-gssapi options, --haproxy-protocol, --haproxy-clientip and --suppress-connect-headers

## Goal

`--proxy1.0 <host>`, `--preproxy <url>`, `--socks5-basic`, `--socks5-gssapi`, `--socks5-gssapi-service <name>`, `--socks5-gssapi-nec`, `--haproxy-protocol`, `--haproxy-clientip <ip>` and `--suppress-connect-headers` parse into `CommandLineOptions` as curl 8.21.0 reads them, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Behaviour: BL-613 to BL-616.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`. `--proxy1.0` sets the proxy as `-x` does (`CommandLineProxy.cs`) and marks it HTTP/1.0; `--preproxy` takes a SOCKS URL (`Curl.Core.UnitLibrary/ProxyUrlParser.cs` reads proxy URLs).

## Acceptance criteria

- [x] Every option and `--no-` form the alias table allows is covered by `Curl.Cli.UnitTests`; `--proxy1.0` and `-x` interplay (last wins or not) is measured with `Record-CurlExchange.ps1` and pinned.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured with the reference curl 8.21.0 (Schannel) on 2026-09-28.
  - Blank value: `--proxy1.0 ''`, `--preproxy ''` and `--haproxy-clientip ''` exit 2 with
    `blank argument where content is expected`; `--socks5-gssapi-service ''` is accepted
    (so it is `AcceptingEmpty`, unlike `--proxy-service-name ''`, which is refused).
  - `--no-proxy1.0`, `--no-preproxy`, `--no-socks5-gssapi-service`, `--no-haproxy-clientip`
    exit 2 as not reversible; `--no-socks5-basic`, `--no-socks5-gssapi`,
    `--no-socks5-gssapi-nec`, `--no-haproxy-protocol`, `--no-suppress-connect-headers` are
    accepted, and `--socks5-basic=x` is accepted with its value ignored.
  - `Record-CurlExchange.ps1 -Port 18612` with `-p`, reading the CONNECT line the proxy got:
    `--proxy1.0 A` -> `CONNECT example.test:80 HTTP/1.0`; `--proxy1.0 A -x B` -> `HTTP/1.1`;
    `-x A --proxy1.0 B` -> `HTTP/1.0`; `--socks5 A --proxy1.0 B` -> `HTTP/1.0`;
    `--proxy1.0 A --socks5 B` -> SOCKS5 (exit 97); `--proxy1.0 http://A` and
    `--proxy1.0 HTTP://A` -> `HTTP/1.0`; `--proxy1.0 socks5://A` -> SOCKS5 (exit 97);
    `--proxy1.0 bogus://h:1` -> exit 7 `Unsupported proxy scheme`.
- So `--proxy1.0` shares `CommandLineOptions.Proxy` with `-x` and the `--socks` options
  (last wins, value and kind together) as `ProxyKind.Http10`, and `CommandLineProxy.TryGetKind`
  keeps `Http10` for an `http://` scheme.
- Defaults taken: each option gets its own property (`PreProxy`, `Socks5BasicAuth`,
  `Socks5GssapiAuth`, `Socks5GssapiServiceName`, `Socks5GssapiNec`, `HaproxyProtocol`,
  `HaproxyClientIp`, `SuppressConnectHeaders`); all nine are per-`--next`-group options, as
  they are per-operation fields in curl's tool. What each does on the wire is BL-613 to BL-616.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --proxy1.0, --preproxy, --socks5-basic, --socks5-gssapi, --socks5-gssapi-service, --socks5-gssapi-nec, --haproxy-protocol, --haproxy-clientip and --suppress-connect-headers parse as curl 8.21.0 does
