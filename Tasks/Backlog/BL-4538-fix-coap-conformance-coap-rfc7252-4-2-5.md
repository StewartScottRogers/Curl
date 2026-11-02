---
id: BL-4538
title: Fix Coap conformance: Coap:RFC7252-4.2-5
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExchange.cs, Curl.Extended.Protocol.Coap.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4538 — Fix Coap conformance: Coap:RFC7252-4.2-5

## Goal

After ignoring a malformed Acknowledgement (RFC 7252 sections 3, 4.1 and 4.2: unknown version, token length 9, payload marker with no payload, option nibble 15, Empty message with bytes), curl takes the well-formed piggybacked response to its retransmission and exits 0.

## Context

Found by BL-4524: `CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted` (all six rows) answers curl's first datagram with a malformed ACK echoing its Message ID and every later one with a good piggybacked 2.05; curl retransmits 4 times and exits 28 ("CoAP server did not answer after 4 retransmissions"), so the good answers are not taken. A silent server (`CoapRetransmissionTests`) and a separate response (`CoapConfirmableExchangeTests`) behave, so start in `CoapExchange.AwaitResponseAsync` at the path after a datagram classified `Reply.Ignored`, and in how the datagram channel under it delivers a later datagram.

## Acceptance criteria

- [ ] `CoapMessageFormatTests.MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted` passes for every row.
- [ ] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests/Client` pins the fix (ADR-0482).
- [ ] The `Coap:RFC7252-4.2-5 BL-4527` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.

## Notes

## Log

- 2026-11-02: Created.
