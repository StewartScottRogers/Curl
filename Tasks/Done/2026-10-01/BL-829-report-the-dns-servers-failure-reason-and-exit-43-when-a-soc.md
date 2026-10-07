---
id: BL-829
title: Report the --dns-servers failure reason and exit 43 when a SOCKS proxy resolves the target locally
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-694]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-829 — Report the --dns-servers failure reason and exit 43 when a SOCKS proxy resolves the target locally

## Goal

Through a SOCKS4 or SOCKS5 proxy that resolves the target locally, a target name the `--dns-servers` client cannot resolve fails with the same exit code and text as a direct connect: `Could not resolve host: <host> (<c-ares reason>)`, or exit 43 `Error 43 resolving <host>:<port>` when a c-ares option does not parse.

## Context

- Found in BL-694's review. `TcpConnector` threads `DnsResolution` (addresses and `DnsLookupFailure`) to the direct and proxy resolve paths through `NameResolutionFailure`, but the SOCKS handshake's resolve callback (`TcpConnector.ResolveAsync`, passed to `SocksProxyTunnel.OpenAsync`) still takes only the addresses, so `Socks4Handshake`/`Socks5Handshake` print their plain exit 6 message and lose exit 43.
- ADR-0170 (the `--dns-servers` client), ADR-0084 (SOCKS handshakes).
- Measure first with the c-ares build of curl (BL-643 Notes: Alpine curl 8.22.0 with c-ares 1.34.8 in a WSL chroot at `/root/alp`) through `Record-CurlExchange.ps1 -DnsPort` and a SOCKS proxy, e.g. `-x socks5://<host>:<port> --dns-servers <silent> http://name.example/`, and `--dns-servers bogus`.

## Acceptance criteria

- [x] The c-ares build's stderr and exit code for both cases are measured and copied into Notes.
- [x] `Curl.Networking.UnitTests` pin the measured message and exit code for SOCKS4 and SOCKS5 local resolution with a resolver that explains its failure, and with a bad configuration.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measurements (2026-10-01)

c-ares build (Alpine curl 8.22.0, c-ares 1.34.8, chroot `/root/alp` in WSL, BL-694's setup) through
`Record-CurlExchange.ps1 -Script <socks steps> -Port 15380 -ListenAddress 172.26.96.1 -DnsPort 15373
-DnsResponseCode 3 -Curl wsl.exe -CurlArgs '-u','root','-e','chroot','/root/alp','curl','-sS','-x',
'<kind>://172.26.96.1:15380','--dns-servers',<list>,'http://bl829.example:1/'`. SOCKS4 script: `read`,
`close`; SOCKS5 script: `read 3`, `send \x05\x00`, `read`, `close`.

| Proxy | `--dns-servers` | Proxy saw | Exit | stderr |
| --- | --- | --- | ---: | --- |
| socks4 | `172.26.96.1:15373` (NXDOMAIN) | nothing | 6 | `curl: (6) Could not resolve host: bl829.example (Domain name not found)` |
| socks4 | `bogus` | nothing | 43 | `curl: (43) Error 43 resolving bl829.example:1` |
| socks5 | `172.26.96.1:15373` (NXDOMAIN) | `05 01 00`, answered `05 00` | 6 | `curl: (6) Could not resolve host: bl829.example (Domain name not found)` |
| socks5 | `bogus` | `05 01 00`, answered `05 00` | 43 | `curl: (43) Error 43 resolving bl829.example:1` |

So the SOCKS local resolve reports exactly as a direct connect: the target host and target port.

### Change

The SOCKS resolve callback now returns `DnsResolution` (`TcpConnector.ResolveWithFailureReasonAsync`
directly; the addresses-only `ResolveAsync` wrapper is gone), and `SocksProxyTunnel.CouldNotResolve(host,
port, failure)` goes through `NameResolutionFailure.Describe`, as the direct and proxy paths do. Pinned by
`TcpConnectorTests.FailureReason`'s two SOCKS4/SOCKS5 data-row tests. No option changed, so `--ai-help`
needs nothing. One `Measure-CodeQuality.ps1` run reported a failing member that a rerun did not
(Networking 100/100, 0 failing); the change adds no branch, so it is taken as run-to-run noise.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A SOCKS4/SOCKS5 local resolve through --dns-servers now reports c-ares' reason (exit 6) or exit 43, as a direct connect does
