---
id: BL-1053
title: Share the DNS cache across -:/--next option groups as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-754]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-02
---
# BL-1053 — Share the DNS cache across -:/--next option groups as curl does

## Goal

A later `-:`/`--next` option group answers a host an earlier group resolved from the run's DNS cache, printing curl 8.21.0's `* Hostname H was found in DNS cache` before `Trying`.

## Context

- Measured in BL-754 Notes: `-k https://127.0.0.1:P/a --next https://127.0.0.1:P/b` (second group opens its own connection) printed `* Hostname 127.0.0.1 was found in DNS cache` for the second group.
- Each group builds its own `TcpConnector` (`CurlComposition.CreateTcpConnector`), which keeps the DNS cache for its life (ADR-0113), so the cache is per group today. ADR-0285 shares the connection cache the same way this task would share the DNS cache: one run-wide object handed to every group's connector.
- Check how `--resolve` entries of different groups interact with one shared cache before deciding (measure it).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: a host resolved in group 1 and connected again in group 2, with and without a `--resolve` entry in group 2; the `-v` lines copied into Notes.
- [x] `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the shared cache and the measured `-v` lines.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Port 18531
-Connections 2`, every response `Connection: close` so group 2 opens its own connection. Group 2's
lines up to `Trying`:

- `-sv http://localhost:18531/a --next -sv http://localhost:18531/b`:
  `* Hostname localhost was found in DNS cache` / `* Host localhost:18531 was resolved.` /
  `* IPv6: ::1` / `* IPv4: 127.0.0.1` / `*   Trying [::1]:18531...`
- the same with `--resolve localhost:18531:127.0.0.1` in group 2:
  `* RESOLVE localhost:18531 - old addresses discarded` / `* Added localhost:18531:127.0.0.1 to DNS cache` /
  `* Hostname localhost was found in DNS cache` / `* Host localhost:18531 was resolved.` /
  `* IPv6: (none)` / `* IPv4: 127.0.0.1` / `*   Trying 127.0.0.1:18531...`
- `-sv --resolve foo.example:18531:127.0.0.1 http://foo.example:18531/a --next -sv http://foo.example:18531/b`
  (and the `+foo.example` non-permanent form): group 2, with no `--resolve`, prints
  `* Hostname foo.example was found in DNS cache` / `* Host foo.example:18531 was resolved.` /
  `* IPv6: (none)` / `* IPv4: 127.0.0.1`; the `Added` line is printed once, by group 1.
- `-sv http://127.0.0.1:18531/a --next -sv http://127.0.0.1:18531/b`: group 1 prints no cache line,
  group 2 `* Hostname 127.0.0.1 was found in DNS cache` before `Trying`.

So curl's cache is run-wide and a group's `--resolve` entries load into it. Implemented as ADR-0351:
a public `DnsCache` in `Curl.Networking` (the dictionary `TcpConnector` kept privately), taken by
`TcpConnector` as an optional `dnsCache` and exposed as `TcpConnector.DnsCache`;
`CurlComposition.CreateRunner` makes one per run and hands it through `CreateTransports` and
`CreateTcpConnector` (`runDnsCache`) to every group. Each group still loads its own `--resolve`
entries per transfer. Tests: `TcpConnectorTests.SharedDnsCache` (6) and
`CurlCommandRunnerSharedDnsCacheTests` (5).

Choice (sensible default, not measured): groups differing in `-4`/`-6` share answers too, keyed on
host and port only, as within one group; recorded in ADR-0351's Consequences.

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Every --next option group answers from the run's one DnsCache, printing curl's 'Hostname H was found in DNS cache' for a host an earlier group resolved
