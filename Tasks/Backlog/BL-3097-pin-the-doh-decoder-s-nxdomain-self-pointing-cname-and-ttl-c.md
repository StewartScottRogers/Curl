---
id: BL-3097
title: Pin the DoH decoder's NxDomain, self-pointing CNAME and TTL cap results in focused DnsAnswerDecoder tests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3011, BL-3012]
touches: [Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3097 — Pin the DoH decoder's NxDomain, self-pointing CNAME and TTL cap results in focused DnsAnswerDecoder tests

## Goal

`DnsAnswerDecoder.Decode` is pinned by focused tests to return what curl 8.22.0's `doh_resp_decode` returns for unit1650's NXDOMAIN, self-pointing CNAME and TTL-above-86400 rows.

## Context

Decoder half of BL-2969 (ADR-0738). The code landed in 8677cc087 (NXDOMAIN, TTL cap) and BL-3011 (CNAME data skipped). Check what `Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs` already pins (`Decode_AnNxdomainRcode_FailsWithNxDomain`, `Decode_AnAnswerTtlAboveOneDay_IsCappedAtOneDay`, BL-3011's CNAME row test), add whichever of the three is missing, and tick the rest. Upstream: https://github.com/curl/curl/blob/curl-8_22_0/lib/vdns/doh.c, `tests/unit/unit1650.c`.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs` has tests that `Decode` gives `NxDomain` for RCODE 3, `None` for unit1650's self-pointing CNAME row (46 bytes, `\x07\x03any\xc0\x27\x00`), and a TTL of 86400 for an answer TTL above 86400.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Networking.UnitTests`.

## Notes

- Split from BL-2969 (over its cost cap); BL-2969's first criterion.

## Log

- 2026-10-22: Created.
