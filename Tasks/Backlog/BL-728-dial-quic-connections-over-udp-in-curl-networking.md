---
id: BL-728
title: Dial QUIC connections over UDP in Curl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-721, BL-727]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-728 — Dial QUIC connections over UDP in Curl.Networking

## Goal

`Curl.Networking.UnitLibrary` dials QUIC: a thin UDP datagram adapter behind a seam (like `ITcpDialer`, ADR-0083) implementing BL-721's datagram channel, and a QUIC connector that resolves the host (honouring `-4`/`-6`, `--resolve`, `--connect-to`, `--interface`/`--local-port` where set), runs the `Curl.Quic` handshake with the hand-built TLS client under `--connect-timeout`, and returns the multiplexed connection, with the `-v` connect lines curl's build prints.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Design: BL-718's ADR. Builds on BL-721 (contracts) and BL-727 (a complete QUIC connection). `Curl.Networking.UnitLibrary` gains references to `Curl.Quic.UnitLibrary` and `Curl.Tls.UnitLibrary` (allowed: it is not a protocol library). Certificate verification reuses the code `SslStreamTlsProvider` uses (BL-708 did the same for TCP).
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs` (resolution, `-4`/`-6`, timing reports for `%{time_connect}` and `%{time_appconnect}`, ADR-0091/ADR-0100), `ConnectToMappings.cs`, `PoolingConnector.cs` (a QUIC connection is shared by its streams).
- The `-v` lines for a QUIC connect are measured in BL-718 (record them there if missing).

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` through the datagram seam complete a dial against an in-memory QUIC server, pin the `-v` connect lines and the timing report, and fail a connect timeout (exit 28) and a handshake failure (the exit BL-718's ADR maps) as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
