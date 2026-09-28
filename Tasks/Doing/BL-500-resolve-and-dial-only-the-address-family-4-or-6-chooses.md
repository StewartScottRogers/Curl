---
id: BL-500
title: Resolve and dial only the address family -4 or -6 chooses
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-499]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-500 — Resolve and dial only the address family -4 or -6 chooses

## Goal

With `-4` the connector uses only IPv4 addresses and with `-6` only IPv6 addresses, for every scheme and for proxies, and a host with no address of that family fails with the exit code and message curl 8.21.0 gives.

## Context

- Conformance audit 2026-09-28, row 4 (Blocker). Parsing is BL-499.
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs`, `SystemDnsResolver.cs`, `ResolveOverrides.cs` (`--resolve` entries of the other family), `UdpDatagramConnector.cs` (TFTP), and the composition in `Curl.Console/CurlTransports.cs`.
- A literal address of the wrong family (`-4 http://[::1]:<P>/`) and a name resolving only to the other family are the two cases to measure.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `-4` and `-6` against `127.0.0.1`, `[::1]` and `localhost`, with `-v`; stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Networking.UnitTests` tests with a fake `IDnsResolver`/`ITcpDialer` show only addresses of the chosen family dialled, and the measured exit code and message (as `ConnectResult`) when none is left.
- [ ] The UDP connector (TFTP) honours the family too, with a test.
- [ ] A `Curl.Console.UnitTests` test shows the family reaching the connector from the command line.
- [ ] New tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
