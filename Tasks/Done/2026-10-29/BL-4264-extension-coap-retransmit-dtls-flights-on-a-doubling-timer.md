---
id: BL-4264
title: Extension CoAP: retransmit DTLS flights on a doubling timer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Coap.UnitLibrary/Dtls/Retransmission, Curl.Extension.Coap.UnitTests/Dtls/Retransmission, Curl.Extension.Coap.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4264 — Extension CoAP: retransmit DTLS flights on a doubling timer

## Goal

`Curl.Extension.Coap.UnitLibrary` resends a DTLS flight on a timer that starts at 1 s and doubles up to 60 s (RFC 6347 section 4.2.4), driven by `TimeProvider`, and gives up after a retransmission limit.

## Context

Split from BL-4251 for size (extension 15 of 63, BL-4116). Extension rules: ADR-0999; it references only `Curl.Protocol.Abstractions`, the shared layers and `Curl.Extension.Abstractions`, and is tested against the RFCs, not curl.

Namespace `Curl.Extension.Coap.Dtls.Retransmission`. A flight is the list of datagrams last sent; the start of the peer's next flight stops the timer. Pick the retransmission limit and record it in an ADR marked "Decided by Claude under Stewart's delegation". BL-4251's handshake drives it; the caller turns giving up into exit 35.

## Acceptance criteria

- [x] Unit tests with a fake `TimeProvider` show the flight resent after 1 s, then 2 s, then 4 s (doubling), the interval capped at 60 s, and no resend once a reply arrives.
- [x] Unit tests show the retransmitter giving up after its retransmission limit.
- [x] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `DtlsFlightRetransmitter.SendFlightAsync(flight, awaitNextFlightAsync, token)` sends the flight, then races a
  `Task.Delay` on the `TimeProvider` against the caller's "peer's next flight started" task; the limit is 12 resends
  (OpenSSL's `DTLS1_TMO_ALERT_COUNT`), recorded in ADR-1061. Giving up throws `TimeoutException`, which BL-4251's
  handshake turns into exit 35.
- Tests reuse `Client/ImmediateTimeProvider` (fires every timer at once, records due times): the run that gives up
  shows due times 1, 2, 4, 8, 16, 32 then 60 seven times.
- Added `Curl.Extension.Coap.UnitLibrary/CLAUDE.md` to touches to describe the new folder; no task in Doing on
  origin/work/dark-factory names it.
- Measure-CodeQuality.ps1 -Library Curl.Extension.Coap.UnitLibrary: 0 failing members. Platform-neutral (no paths,
  OS text or real time); Coap tests 159/159 green.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 6670888c 7951e05f 54cafd17 3d836d5e.
- 2026-10-29: Doing -> Done. CoAP DTLS flights resend on a 1 s timer doubling to 60 s and give up after 12 resends
