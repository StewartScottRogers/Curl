---
id: BL-413
title: Apply --resolve and --connect-to to the UDP connector TFTP uses
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-244]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-413 — Apply --resolve and --connect-to to the UDP connector TFTP uses

## Goal

A `tftp://` transfer resolves through `--resolve` entries and follows `--connect-to` mappings, as curl 8.21.0 does for every connection it makes.

## Context

- Found in BL-244: `CurlComposition.CreateTcpConnector` hands `ResolveOverrides` and `ConnectToMappings` to `TcpConnector`, but `UdpDatagramConnector` (Curl.Networking.UnitLibrary) takes only an `IDnsResolver`, so TFTP still asks the system resolver and dials the URL's host and port.
- Measure real curl 8.21.0 with `Record-CurlExchange.ps1` (or a loopback UDP responder added to it) before pinning behaviour: whether `--connect-to` applies to TFTP, and the exit 49 messages for a malformed entry.

## Acceptance criteria

- [ ] A `Curl.Networking.UnitTests` test shows `UdpDatagramConnector` opening its channel at a `--resolve` address and, if measured curl applies it, at a `--connect-to` mapped host and port.
- [ ] A `Curl.Console.UnitTests` test shows `CurlComposition.CreateTransports` builds the UDP connector with the command line's `--resolve` and `--connect-to` values.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage with no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console` (`-IncludeIntegration` for Curl.Console).

## Notes

## Log

- 2026-09-27: Created.
