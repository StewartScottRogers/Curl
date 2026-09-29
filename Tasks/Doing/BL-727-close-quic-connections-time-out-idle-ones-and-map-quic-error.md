---
id: BL-727
title: Close QUIC connections, time out idle ones and map QUIC errors to curl's exit codes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-725, BL-726]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-727 — Close QUIC connections, time out idle ones and map QUIC errors to curl's exit codes

## Goal

The QUIC connection closes as curl's build closes it (CONNECTION_CLOSE with the application or transport code, closing and draining periods), times out after the negotiated idle timeout, recognises a stateless reset, and turns every transport error, TLS alert and timeout into the typed failure BL-718's ADR maps to curl's exit codes (96 `CURLE_QUIC_CONNECT_ERROR`, 95 `CURLE_HTTP3`, 28 timeout).

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-725 and BL-726. References: RFC 9000 section 10 (idle timeout, immediate close, stateless reset), 20 (error codes); RFC 9001 section 4.8 (TLS alerts as `CRYPTO_ERROR`).
- The failure messages `-v` and the error line print are measured in BL-718 (record them there if missing).

## Acceptance criteria

- [ ] `Curl.Quic.UnitTests` pin the CONNECTION_CLOSE sent at the end of a normal connection, the idle timeout on a fake `TimeProvider`, detection of a stateless reset token, and the typed failure and mapped exit for a peer CONNECTION_CLOSE with a transport error, a TLS alert, and a handshake that never completes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
