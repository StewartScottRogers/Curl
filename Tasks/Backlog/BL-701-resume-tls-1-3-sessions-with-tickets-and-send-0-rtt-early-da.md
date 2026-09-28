---
id: BL-701
title: Resume TLS 1.3 sessions with tickets and send 0-RTT early data
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-699]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-701 — Resume TLS 1.3 sessions with tickets and send 0-RTT early data

## Goal

The hand-built TLS 1.3 client stores NewSessionTicket tickets in an exportable session record, resumes with a PSK (`psk_dhe_ke`) and binder, and, when asked, sends 0-RTT early data and handles its acceptance or rejection, so `--ssl-sessions` and `--tls-earlydata` (BL-710) and QUIC 0-RTT can use them.

## Context

- Design: BL-695's ADR (the session record's format, which is what `--ssl-sessions` writes to its file). Builds on BL-699 and BL-697 (resumption and binder secrets).
- Reference: RFC 8446 sections 2.2, 2.3, 4.2.10, 4.2.11, 4.6.1; RFC 8448 section 4 (resumed 0-RTT handshake trace, with the ticket from section 3).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` replay RFC 8448 section 4 as the client (ClientHello with PSK and binder byte for byte, early data accepted), resume against the in-memory server with an exported and re-imported session record, and handle early data rejected by the server (data resent after the handshake) and an expired ticket (full handshake).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
