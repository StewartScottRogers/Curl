---
id: BL-700
title: Protect TLS 1.3 records over a byte stream in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-699]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-700 — Protect TLS 1.3 records over a byte stream in the hand-built TLS client

## Goal

The hand-built client runs TLS 1.3 over a byte stream: the record layer (RFC 8446 section 5: content types, AEAD protection with per-record nonces, padding, the 2^14 limit, change_cipher_spec compatibility), alerts, `close_notify`, KeyUpdate, and a `Stream` the rest of Curl can read and write like the one `SslStream` gives.

## Context

- Design: BL-695's ADR. Builds on BL-699 (handshake). The byte stream is whatever the caller hands in (in production, the TCP `IConnection`'s stream, wired by BL-708); this library never opens a socket.
- Reference: RFC 8448 section 3 (the protected records in the trace, including the application data exchange and the alerts).

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` reproduce RFC 8448 section 3's protected records byte for byte, exchange application data both ways with the in-memory server over a pipe for each suite, handle a KeyUpdate from the server, treat a missing `close_notify` as curl does (record the rule from BL-695's ADR), and fail a corrupted record with `bad_record_mac`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Missing `close_notify` (ADR-0157; BL-695's ADR is ADR-0140, which names `close_notify` but not the rule): `Tls13ClientStream` returns 0 as `SslStream` does and exposes `CloseNotifyReceived`; curl's exit 56 for a transfer left unfinished is the caller's mapping, noted on BL-708.
- "Each suite" is the three the record layer can protect today (0x1301-0x1303). The CCM suites wait for BL-738's hand-built AES-CCM; filed BL-811 for their TLS 1.3 records. Until then `ConnectAsync` refuses them before sending anything.
- `ITls12Aead` and its implementations are renamed `ITlsAead` / `*TlsAead`, since TLS 1.3 records share them.
- Measured 2026-09-28: `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` 100% line, 100% branch, 605 members, 0 failing, worst CRAP 10. Solution build clean, fast tests green.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The hand-built client runs TLS 1.3 over a byte stream: protected records, alerts, close_notify, KeyUpdate and an SslStream-like Tls13ClientStream
