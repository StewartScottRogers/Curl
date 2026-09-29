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
completed: 2026-09-29
---
# BL-701 — Resume TLS 1.3 sessions with tickets and send 0-RTT early data

## Goal

The hand-built TLS 1.3 client stores NewSessionTicket tickets in an exportable session record, resumes with a PSK (`psk_dhe_ke`) and binder, and, when asked, sends 0-RTT early data and handles its acceptance or rejection, so `--ssl-sessions` and `--tls-earlydata` (BL-710) and QUIC 0-RTT can use them.

## Context

- Design: BL-695's ADR (the session record's format, which is what `--ssl-sessions` writes to its file). Builds on BL-699 and BL-697 (resumption and binder secrets).
- Reference: RFC 8446 sections 2.2, 2.3, 4.2.10, 4.2.11, 4.6.1; RFC 8448 section 4 (resumed 0-RTT handshake trace, with the ticket from section 3).

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` replay RFC 8448 section 4 as the client (ClientHello with PSK and binder byte for byte, early data accepted), resume against the in-memory server with an exported and re-imported session record, and handle early data rejected by the server (data resent after the handshake) and an expired ticket (full handshake).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- The session record and its `SSL_SESSION` DER codec follow ADR-0140 (BL-695); no new ADR was needed.
- `Tls13ClientConnection.ConnectWithEarlyDataAsync` takes the first application data: up to the ticket's `max_early_data_size` goes as 0-RTT right after the ClientHello, and whatever the server did not accept is written after the handshake, before the stream is returned. A HelloRetryRequest drops early data and keeps the ticket only when the chosen suite shares its hash (RFC 8446 4.1.2, 4.2.10).
- The resumed run was cut off with two connection tests hanging: the test server's reader counted a skipped (rejected early data) record in its sequence number, so the client's Finished never opened. The test server now reinstalls its reader after a skip.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The TLS 1.3 client records tickets as exportable sessions, resumes with psk_dhe_ke and a binder, and sends 0-RTT early data, resending it when rejected
