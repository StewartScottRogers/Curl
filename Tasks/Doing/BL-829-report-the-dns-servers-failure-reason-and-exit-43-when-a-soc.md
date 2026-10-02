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
completed:
---
# BL-829 — Report the --dns-servers failure reason and exit 43 when a SOCKS proxy resolves the target locally

## Goal

Through a SOCKS4 or SOCKS5 proxy that resolves the target locally, a target name the `--dns-servers` client cannot resolve fails with the same exit code and text as a direct connect: `Could not resolve host: <host> (<c-ares reason>)`, or exit 43 `Error 43 resolving <host>:<port>` when a c-ares option does not parse.

## Context

- Found in BL-694's review. `TcpConnector` threads `DnsResolution` (addresses and `DnsLookupFailure`) to the direct and proxy resolve paths through `NameResolutionFailure`, but the SOCKS handshake's resolve callback (`TcpConnector.ResolveAsync`, passed to `SocksProxyTunnel.OpenAsync`) still takes only the addresses, so `Socks4Handshake`/`Socks5Handshake` print their plain exit 6 message and lose exit 43.
- ADR-0170 (the `--dns-servers` client), ADR-0084 (SOCKS handshakes).
- Measure first with the c-ares build of curl (BL-643 Notes: Alpine curl 8.22.0 with c-ares 1.34.8 in a WSL chroot at `/root/alp`) through `Record-CurlExchange.ps1 -DnsPort` and a SOCKS proxy, e.g. `-x socks5://<host>:<port> --dns-servers <silent> http://name.example/`, and `--dns-servers bogus`.

## Acceptance criteria

- [ ] The c-ares build's stderr and exit code for both cases are measured and copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin the measured message and exit code for SOCKS4 and SOCKS5 local resolution with a resolver that explains its failure, and with a bad configuration.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
