---
id: BL-466
title: Resolve a host or interface name given to -P/--ftp-port in FtpProtocolHandler
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-437]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed:
---
# BL-466 — Resolve a host or interface name given to -P/--ftp-port in FtpProtocolHandler

## Goal

`FtpProtocolHandler` resolves a host name given to `-P`/`--ftp-port` (and, if a seam allows, an interface name) to the address it listens on, as curl 8.21.0 does, instead of ending every name with exit 6.

## Context

- BL-437 implemented `-P` for `-`, IPv4 and IPv6 literals only; a name ends with exit 6, `Could not resolve host: <name>`, and no `QUIT`, which matches curl only for a name that does not resolve (ADR-0102, BL-437 addendum).
- The handler has no DNS seam. Decide (ADR, Claude under Stewart's delegation) whether `IConnectionListener`/`ListenTarget` takes a host name or the handler gets a resolver seam; that may need a prerequisite abstractions task.
- Measure with `Record-CurlExchange.ps1 -Ftp` (it dials back to the `EPRT`/`PORT` address): `-P localhost`, `-P <interface>` and a name that does not resolve.

## Acceptance criteria

- [ ] An ADR records how a `-P` name reaches a listening address, and any prerequisite task is filed and listed in `depends-on`.
- [ ] Named tests in `Curl.Protocol.Ftp.UnitTests` pin curl 8.21.0's `EPRT` for `-P localhost` and its exit code and message for a name that does not resolve.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-437 (lane 3, 2026-09-27).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
