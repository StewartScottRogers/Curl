---
id: BL-643
title: Parse --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr on every platform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-643 — Parse --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr on every platform

## Goal

The four c-ares options parse into `CommandLineOptions` on every platform with the value checks a c-ares build of curl 8.21.0 applies (server list syntax, interface name, IPv4 and IPv6 address), instead of `is unknown`; resolving through them is BL-694.

## Context

- Conformance audit 2026-09-28, row 28 (Minor). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): curl's c-ares builds support these options, so Curl supports them on every platform, with a hand-built DNS client (BL-694); a build without c-ares refusing them is recorded in Notes for information only.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; option texts in `CurlManual.txt`. Malformed values are refused in `CommandLineRefusal.cs` with the text a c-ares build prints.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` and a c-ares build of curl (for example a Linux distribution build that lists `AsynchDNS` with c-ares in `curl -V`): each option with a valid and a malformed value against a loopback URL; stdout, stderr, exit code copied into Notes; the reference builds' refusals, if any, recorded too.
- [x] `Curl.Cli.UnitTests` pin each option parsing and each malformed-value refusal on every platform (no `OSCondition` refusal).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measurements (2026-09-28)

c-ares build: Alpine edge `curl 8.22.0 (x86_64-alpine-linux-musl) libcurl/8.22.0 OpenSSL/3.5.8 ... c-ares/1.34.8`,
installed with `apk` into an Alpine minirootfs chroot at `/tmp/alp` in WSL and run through
`Record-CurlExchange.ps1 -NoServer -Curl wsl.exe -CurlArgs '-u','root','-e','chroot','/tmp/alp','curl','-sS',...`
(no recorder change was needed). No 8.21.0 c-ares build was at hand; 8.22.0 is the nearest.

Against `http://127.0.0.1:1/` (an IP literal, so nothing is resolved), stdout empty in every case:

| Case | Exit | stderr |
| --- | ---: | --- |
| `--dns-servers 192.168.0.1,192.168.0.2`, `10.0.0.1:53`, `[::1]:53,1.2.3.4` | 7 | `curl: (7) Failed to connect to 127.0.0.1:1 after 0 ms: Could not connect to server` |
| `--dns-servers bogus`, `1.2.3.4:99999`, `1.2.3.4,`, `1.2.3.4, 5.6.7.8` | 7 | same |
| `--dns-interface eth0`, `nosuchif0` | 7 | same |
| `--dns-ipv4-addr 10.1.2.3`, `bogus`, `::1` | 7 | same |
| `--dns-ipv6-addr 2a04:4e42::561`, `bogus`, `1.2.3.4` | 7 | same |
| `--dns-servers ''` (and each other option with `''`) | 2 | `curl: option --dns-servers: blank argument where content is expected` + `curl: try 'curl --help' or 'curl --manual' for more information` |
| `--no-dns-servers x` | 2 | `curl: option --no-dns-servers: the given option cannot be reversed with a --no- prefix` + try line |

Against `http://bl643.example:1/` (a name c-ares must resolve), `-m 8`:

| Case | Exit | stderr |
| --- | ---: | --- |
| `--dns-servers 127.0.0.1:1` | 6 | `curl: (6) Could not resolve host: bl643.example (Could not contact DNS servers)` |
| `--dns-servers bogus`, `1.2.3.4:99999` | 43 | `curl: (43) Error 43 resolving bl643.example:1` |
| `--dns-interface nosuchif0` | 6 | `curl: (6) Could not resolve host: bl643.example (DNS server returned general failure)` |
| `--dns-ipv4-addr bogus`, `::1`; `--dns-ipv6-addr bogus`, `1.2.3.4` | 43 | `curl: (43) Error 43 resolving bl643.example:1` |
| `--dns-ipv4-addr 127.0.0.1 --dns-servers 127.0.0.1:1` | 6 | `curl: (6) Could not resolve host: bl643.example (Could not contact DNS servers)` |

Reference builds without c-ares (for information only): Git for Windows' Schannel curl 8.21.0 and
WSL Ubuntu's OpenSSL curl 8.18.0 refuse every one of these options, with any value, exit 2:
`curl: option --dns-servers: the installed libcurl version does not support this` + the try line.

### Decisions

- The only parse-time value check a c-ares build applies is the blank refusal, so the four options are
  `CommandLineOption.Text` rows storing the value verbatim (`DnsServers`, `DnsInterface`, `DnsIPv4Address`,
  `DnsIPv6Address`). The malformed-value refusal (exit 43, `Error 43 resolving <host>:<port>`) happens
  when a name is resolved, so it belongs to the resolver, BL-694, not to `CommandLineRefusal`. This is
  matching measured curl, not a new design choice, so no ADR was written.
- Per-group, not global: curl keeps them in `OperationConfig`; pinned in `CommandLineNextGroupTests`.
- `Curl.Console.UnitTests` needed no change: no test there depended on these options being unknown, and its fast tests pass unchanged.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr parse on every platform, refusing only a blank value as a c-ares curl does
