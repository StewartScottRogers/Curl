---
id: BL-4723
title: Fix DnsQuery conformance: DoQ must refuse UDP port 53 (RFC 9250 4.1.1)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4722]
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Quic/DnsQuicTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Quic, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted/DnsOverQuicCases.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4723 — Fix DnsQuery conformance: DoQ must refuse UDP port 53 (RFC 9250 4.1.1)

## Goal

Curl never opens a DNS over QUIC connection to UDP port 53: `dns://host:53/... --dns-transport quic` fails without a datagram, as RFC 9250 section 4.1.1 says ("DoQ connections MUST NOT use UDP port 53").

## Context

Found by BL-4683: `DnsOverQuicCases.Quic_ExplicitPort53_IsRefusedWithoutAConnection` (`DnsQuery:RFC9250-4.1.1-2`) is listed in `KnownFailures.txt`; curl completes the handshake and answers with exit 0. Refuse in `DnsQuicTransport.ExchangeAsync` before `connector.OpenAsync` when the port is 53; choose the exit code and message by the lane rules (record it in an ADR), then pin the exact exit code and stderr in the case. Depends on BL-4722 only because both change `DnsQuicTransport.cs`.

## Acceptance criteria

- [x] `--dns-transport quic` to port 53 fails with no datagram sent; the case pins its exit code and stderr and its `KnownFailures.txt` line is removed.
- [x] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` covers the refusal; 100% line and branch coverage holds; build clean and fast tests green.

## Notes

- Plan (direct, small change): refuse port 53 in `DnsQuicTransport.ExchangeAsync` before `connector.OpenAsync`, so no datagram is sent. Exit 7 (`CURLE_COULDNT_CONNECT`) with "DNS over QUIC must not use UDP port 53 (RFC 9250 section 4.1.1)", decided in ADR-1233 (upstream curl has no DoQ to measure; exit 3 rejected since the URL is valid for other transports).
- Unit test `ExchangeAsync_Rfc9250Section4_1_1_Port53_FailsWithExit7WithoutOpeningAChannel`; conformance case pins exit 7 and stderr and asserts no query reached the server; its KnownFailures line removed.
- Build clean, fast tests green; Measure-CodeQuality for Curl.Extended.Protocol.DnsQuery.UnitLibrary: 0 failing members. Change is platform-neutral (no paths or OS text), so CI on Linux and macOS is covered by these.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: d9b87036 b16c8440.
- 2026-11-03: Doing -> Done. DoQ refuses UDP port 53 with exit 7 before any datagram (RFC 9250 4.1.1)
