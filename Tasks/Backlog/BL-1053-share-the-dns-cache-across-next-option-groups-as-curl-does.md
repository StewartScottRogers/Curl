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
completed:
---
# BL-1053 — Share the DNS cache across -:/--next option groups as curl does

## Goal

A later `-:`/`--next` option group answers a host an earlier group resolved from the run's DNS cache, printing curl 8.21.0's `* Hostname H was found in DNS cache` before `Trying`.

## Context

- Measured in BL-754 Notes: `-k https://127.0.0.1:P/a --next https://127.0.0.1:P/b` (second group opens its own connection) printed `* Hostname 127.0.0.1 was found in DNS cache` for the second group.
- Each group builds its own `TcpConnector` (`CurlComposition.CreateTcpConnector`), which keeps the DNS cache for its life (ADR-0113), so the cache is per group today. ADR-0285 shares the connection cache the same way this task would share the DNS cache: one run-wide object handed to every group's connector.
- Check how `--resolve` entries of different groups interact with one shared cache before deciding (measure it).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: a host resolved in group 1 and connected again in group 2, with and without a `--resolve` entry in group 2; the `-v` lines copied into Notes.
- [ ] `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the shared cache and the measured `-v` lines.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-30: Created.
