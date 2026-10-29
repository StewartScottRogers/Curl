---
id: BL-4264
title: Extension CoAP: retransmit DTLS flights on a doubling timer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Coap.UnitLibrary/Dtls/Retransmission, Curl.Extension.Coap.UnitTests/Dtls/Retransmission]
requirement: none
created: 2026-10-29
completed:
---
# BL-4264 — Extension CoAP: retransmit DTLS flights on a doubling timer

## Goal

`Curl.Extension.Coap.UnitLibrary` resends a DTLS flight on a timer that starts at 1 s and doubles up to 60 s (RFC 6347 section 4.2.4), driven by `TimeProvider`, and gives up after a retransmission limit.

## Context

Split from BL-4251 for size (extension 15 of 63, BL-4116). Extension rules: ADR-0999; it references only `Curl.Protocol.Abstractions`, the shared layers and `Curl.Extension.Abstractions`, and is tested against the RFCs, not curl.

Namespace `Curl.Extension.Coap.Dtls.Retransmission`. A flight is the list of datagrams last sent; the start of the peer's next flight stops the timer. Pick the retransmission limit and record it in an ADR marked "Decided by Claude under Stewart's delegation". BL-4251's handshake drives it; the caller turns giving up into exit 35.

## Acceptance criteria

- [ ] Unit tests with a fake `TimeProvider` show the flight resent after 1 s, then 2 s, then 4 s (doubling), the interval capped at 60 s, and no resend once a reply arrives.
- [ ] Unit tests show the retransmitter giving up after its retransmission limit.
- [ ] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 6670888c 7951e05f 54cafd17 3d836d5e.
