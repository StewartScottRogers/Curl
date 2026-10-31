---
id: BL-4391
title: Map WireGuard handshake and tunnel DNS failures to exit 7 with their reason
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardConnector.cs, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardDatagramConnector.cs, Curl.Extension.WireGuard.UnitTests/Tunnel]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4391 — Map WireGuard handshake and tunnel DNS failures to exit 7 with their reason

## Goal

Every failure to reach a WireGuard peer - including a SocketException during the handshake and a failed DNS lookup through the tunnel - ends with exit 7 and a message naming the tunnel failure, never an unhandled exception or a misleading exit 6.

## Context

Shift-end review 2026-10-31 (code-reviewer, silent-failure-hunter). Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardConnector.cs:212 maps only TimeoutException or IOException to Unreachable; a SocketException (for example Windows 10054 after ICMP port unreachable) is rethrown and crashes curl. Same in WireGuardDatagramConnector.cs (around 311-314) and the tunnel DNS path (around 149). ResolveThroughTunnelAsync (124-171) turns a failed tunnel connection, an IOException in ExchangeAsync (167) and an unencodable query (144) into null, so a dead peer gives "Could not resolve host" exit 6 instead of the exit 7 ConnectThroughAsync gives for an IP literal.

## Acceptance criteria

- [x] A SocketException from the UDP socket during the handshake gives exit 7 with the "through the WireGuard tunnel" message, for both the TCP and the datagram connector, proven by tests in Curl.Extension.WireGuard.UnitTests/Tunnel.
- [x] With an unreachable peer, `--wireguard wg.conf https://example.com` (a host name) exits 7 with the tunnel failure, not 6, proven by a test.
- [x] dotnet build is clean, the fast tests pass and the library keeps 100% line and branch coverage.

## Notes

- DNS through the tunnel now gives a `TunnelResolution` (address, or the tunnel failure): a dead peer, no peer routing the DNS server, or an IOException during the exchange is exit 7; no DNS server, an unencodable name or no answer stays exit 6. SocketException counts as a tunnel failure in both connectors (`IsTunnelFailure`). Quality run: 100% line and branch, complexity within gate.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 815246ea c69aaf5a f874b647.
- 2026-10-31: Doing -> Done. WireGuard SocketException and tunnel DNS failures now exit 7; tests added, 100% coverage
