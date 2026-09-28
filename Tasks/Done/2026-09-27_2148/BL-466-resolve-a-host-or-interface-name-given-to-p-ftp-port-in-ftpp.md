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
completed: 2026-09-27
---
# BL-466 — Resolve a host or interface name given to -P/--ftp-port in FtpProtocolHandler

## Goal

`FtpProtocolHandler` resolves a host name given to `-P`/`--ftp-port` (and, if a seam allows, an interface name) to the address it listens on, as curl 8.21.0 does, instead of ending every name with exit 6.

## Context

- BL-437 implemented `-P` for `-`, IPv4 and IPv6 literals only; a name ends with exit 6, `Could not resolve host: <name>`, and no `QUIT`, which matches curl only for a name that does not resolve (ADR-0102, BL-437 addendum).
- The handler has no DNS seam. Decide (ADR, Claude under Stewart's delegation) whether `IConnectionListener`/`ListenTarget` takes a host name or the handler gets a resolver seam; that may need a prerequisite abstractions task.
- Measure with `Record-CurlExchange.ps1 -Ftp` (it dials back to the `EPRT`/`PORT` address): `-P localhost`, `-P <interface>` and a name that does not resolve.

## Acceptance criteria

- [x] An ADR records how a `-P` name reaches a listening address, and any prerequisite task is filed and listed in `depends-on`.
- [x] Named tests in `Curl.Protocol.Ftp.UnitTests` pin curl 8.21.0's `EPRT` for `-P localhost` and its exit code and message for a name that does not resolve.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-437 (lane 3, 2026-09-27).
- Decision (ADR-0108): no prerequisite task. `IDnsResolver` already exists in `Curl.Protocol.Abstractions.UnitLibrary`, so `FtpProtocolHandler` gains a constructor taking one; the handler resolves the name and hands the listener an `IPAddress`, and `ListenTarget` is unchanged. The first resolved address is used, IPv4-mapped announced as IPv4. The older constructors resolve nothing (`UnavailableDnsResolver`).
- Measured 2026-09-27, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Ftp`, `curl -v -P <value> ftp://127.0.0.1:47466/f.txt`: `localhost` -> `EPRT |2|::1|58064|`, exit 0; `nosuch.invalid` and `"Loopback Pseudo-Interface 1"` -> `* Could not resolve host: <name>`, `* failed to resolve the address provided to PORT: <name>`, no `QUIT`, exit 6. The handler now reports both `-v` lines.
- Tests: `ExecuteAsync_PortLocalhost_ListensOnAndAnnouncesTheFirstResolvedAddress`, `ExecuteAsync_PortNameDoesNotResolve_ReportsCurlsTwoLinesAndEndsWithExit6WithoutQuit`, `ExecuteAsync_PortNameResolvesToIpv4Mapped_AnnouncesItAsIpv4WithThePortRange`, `ExecuteAsync_PortLiteral_IsNotResolved`, `Constructor_NullDnsResolver_Throws` in `FtpProtocolHandlerActiveModeTests`.
- Follow-ups: BL-474 filed (interface names via `getifaddrs` off Windows). BL-458 (Console wiring, Backlog) got a Context line to pass the composition's `IDnsResolver`.
- `Tasks/Backlog` edits (BL-474, BL-458) are board housekeeping outside `touches`; no product project outside `touches` changed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. FtpProtocolHandler resolves a -P host name through an injected IDnsResolver (EPRT |2|::1| for localhost), and an unresolvable name reports curl's two -v lines and exit 6 (ADR-0108)
