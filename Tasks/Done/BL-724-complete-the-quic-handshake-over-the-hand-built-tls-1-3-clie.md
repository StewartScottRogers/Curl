---
id: BL-724
title: Complete the QUIC handshake over the hand-built TLS 1.3 client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-723, BL-699, BL-721]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-724 — Complete the QUIC handshake over the hand-built TLS 1.3 client

## Goal

A QUIC client connection in `Curl.Quic.UnitLibrary` completes the handshake over the datagram channel contract: Initial, Handshake and 1-RTT packet number spaces, CRYPTO frames carrying the hand-built TLS 1.3 client's messages, the transport parameters BL-718's ADR records curl's build sending (in the `quic_transport_parameters` extension), ALPN `h3`, connection ID issue and retirement, Retry and Version Negotiation handling, address-validation tokens, HANDSHAKE_DONE and discarding of Initial and Handshake keys.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-723 (protection), BL-699 (TLS 1.3 handshake as a message-level state machine) and BL-721 (datagram channel contract). References: RFC 9000 sections 5, 7, 8, 17.2.5 (Retry), 6 (Version Negotiation), 18 (transport parameters); RFC 9001 section 4.
- Tests drive an in-memory QUIC server built from the same pieces with a generated certificate, over a fake datagram channel, with `TimeProvider` and injected randomness, so the first client Initial for fixed inputs can be pinned.

## Acceptance criteria

- [x] `Curl.Quic.UnitTests` complete a handshake against the in-memory server, pin the first client Initial datagram (padded to 1200 bytes) for fixed inputs, follow a Retry (new token, new connection ID), abort on a Version Negotiation that lists no supported version with the exit BL-718's ADR maps, and fail a transport-parameter error and a TLS alert with the mapped errors.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Design decisions (layers, exit mapping, the ClientHello less `00ff`, packet layout, timeouts) are in ADR-0163, decided by Claude under Stewart's delegation.
- `touches` widened to `Curl.Tls.UnitLibrary` and `Curl.Tls.UnitTests`: QUIC needs the server's `quic_transport_parameters` from EncryptedExtensions, which only the TLS handshake sees, so `Tls13ClientHandshake` now exposes `ServerQuicTransportParameters`. No task in Doing named either project. `Documentation/Planning/Decisions` added for ADR-0163.
- `Curl.Quic.UnitLibrary` now references `Curl.Protocol.Abstractions.UnitLibrary` for `IDatagramChannel` and `CurlExitCode`.
- Follow-ups: BL-819 (offer `00ff` in the QUIC ClientHello, as curl does); a note on BL-725 about coalescing packets for different destination connection IDs.
- Measured at the end: Quic 100%/100%, 312 members, 0 failing; Tls 100%/100%, 653 members, 0 failing. `dotnet format --verify-no-changes` reports only the ENDOFLINE issues in `Tls12CbcRecordTests.cs`, a file this task did not touch.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The QUIC client completes its handshake over the hand-built TLS 1.3 client, with Retry, Version Negotiation, transport parameters and key discard
