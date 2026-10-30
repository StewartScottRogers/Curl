---
id: BL-4324
title: WireGuard extension: user-space IPv4, IPv6 and TCP client stack
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4326]
touches: [Curl.Extension.WireGuard.UnitLibrary/IpStack, Curl.Extension.WireGuard.UnitTests/IpStack]
requirement: none
created: 2026-10-29
completed:
---
# BL-4324 — WireGuard extension: user-space IPv4, IPv6 and TCP client stack

## Goal

`Curl.Extension.WireGuard.UnitLibrary/IpStack` is a user-space IPv4, IPv6 and TCP client stack: it opens one TCP connection to an address inside the tunnel and exposes it as a byte stream, exchanging raw IP packets with a packet channel it is handed.

## Context

Split from BL-4147. The tunnel carries raw IP packets; the transfer above needs a TCP byte stream, so the extension needs its own minimal IP and TCP client. Specifications: RFC 791 (IPv4, header checksum, no fragmentation sent; reassembly may be left out with an ADR line), RFC 8200 (IPv6), RFC 9293 (TCP: three-way handshake, sequence and acknowledgement numbers, MSS option, receive window, retransmission with RFC 6298 timers, FIN and RST handling), RFC 1071 (checksums). Inject `TimeProvider`; no network: tests use a fake packet channel and a scripted peer, or recorded packets. Optionally UDP (RFC 768) for the configuration's `DNS` server.

## Acceptance criteria

- [ ] IPv4 and IPv6 packets are built and parsed with correct header checksums and TCP pseudo-header checksums; malformed or misaddressed packets are dropped; unit tests.
- [ ] A TCP client connects (SYN, SYN-ACK, ACK with MSS), sends and receives data in order across segment boundaries, retransmits a lost segment after the RFC 6298 timeout on a fake `TimeProvider`, and closes with FIN or reports a RST as a connection reset; unit tests.
- [ ] The stream the stack exposes reads and writes asynchronously without `.Result`, `.Wait()` or `Thread.Sleep`; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.WireGuard.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
