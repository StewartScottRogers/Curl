---
id: BL-1027
title: Write curl's -v lines for the --interface and --local-port bind
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-600]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-1027 — Write curl's -v lines for the --interface and --local-port bind

## Goal

`curl -v` with `--interface` or `--local-port` writes the bind lines curl 8.21.0 writes between `Trying` and the connect outcome.

## Context

- Follow-up from BL-600 (ADR-0269, Consequences). BL-600 binds and fails with the right exit codes and
  the `connect to ... from  port 0 failed:` line, but not these lines. Code: `LocalBindingTcpDialer`,
  `TcpDialer.BindLocalEnd`, `AddressFamilyRace` in `Curl.Networking.UnitLibrary`; the dialer has no
  `ITransferEvents` today, so the lines need a way out (return them with the dial, or pass the events).
- Measured on Windows 2026-09-29 (BL-600 Notes), curl 8.21.0 Schannel:
  - `--interface 127.0.0.1 --local-port 40010-40012`: `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`, `Local port: 40010`.
  - `--interface 127.0.0.1` to `localhost`: for `::1` `Name '127.0.0.1' family 23 resolved to '127.0.0.1' family 2`; bound with no port: `Local port: 0`.
  - `--local-port 40020` alone: `Local port: 40020`.
  - `--local-port 40000-40002` all busy: `Bind to local port 40000 failed, trying next`, `Bind to local port 40001 failed, trying next`, `bind failed with errno 10048: Address already in use`.
  - `--interface bogus0`: `Could not resolve host: bogus0`, `Could not bind to 'bogus0' with errno 0: No error`.
  - `--interface if!Ethernet`: `Could not bind to interface 'Ethernet' with errno 0: No error`.
- Family numbers and errno text differ by platform (Linux AF_INET6 is 10, glibc's `strerror`); measure on Linux too.

## Acceptance criteria

- [ ] Each line above is measured again with `Record-CurlExchange.ps1` and pinned in `Curl.Networking.UnitTests` for its case; platform-specific text is pinned under `OSCondition`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
