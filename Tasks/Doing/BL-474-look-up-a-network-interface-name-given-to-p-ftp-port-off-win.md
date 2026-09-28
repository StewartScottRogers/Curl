---
id: BL-474
title: Look up a network interface name given to -P/--ftp-port off Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-466]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed:
---
# BL-474 — Look up a network interface name given to -P/--ftp-port off Windows

## Goal

On Linux and macOS, `FtpProtocolHandler` takes a `-P` value that names a network interface (such as `lo` or `eth0`) as that interface's address, as the OpenSSL builds of curl 8.21.0 do through `Curl_if2ip`, before falling back to resolving it as a host name.

## Context

- BL-466 (ADR-0108) resolves a `-P` name through `IDnsResolver` and treats an interface name as a host name, which matches the Windows (Schannel) build: it has no `getifaddrs`, and `-P "Loopback Pseudo-Interface 1"` was measured to end with exit 6.
- curl 8.21.0's `lib/ftp.c`, `ftp_state_use_port`, calls `Curl_if2ip(conn->remote_addr->family, …)` first: found, it uses that interface's address of the control connection's family; `IF2IP_AF_NOT_SUPPORTED` ends with exit 30; not found, it resolves the name.
- The handler may not call system APIs itself; an interface lookup needs a seam (BCL: `System.Net.NetworkInformation.NetworkInterface`), implemented in `Curl.Networking.UnitLibrary`. Wiring it in `Curl.Console` is a follow-up once BL-458 is done.
- Measure on Linux or macOS with `Record-CurlExchange.ps1 -Ftp`: `-P lo` and a name that is neither an interface nor a host.

## Acceptance criteria

- [ ] An ADR (or an addendum to ADR-0108) records the seam and what curl 8.21.0 was measured to do off Windows.
- [ ] Named tests in `Curl.Protocol.Ftp.UnitTests` pin `EPRT` for an interface name found by the seam, and that on Windows the name still goes to the resolver.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-466 (lane 3, 2026-09-27).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
