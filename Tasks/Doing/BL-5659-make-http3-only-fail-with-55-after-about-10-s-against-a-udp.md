---
id: BL-5659
title: Make --http3-only fail with 55 after about 10 s against a UDP listener that does not speak QUIC, instead of running 300 s
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/QuicDialer.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5659 — Make --http3-only fail with 55 after about 10 s against a UDP listener that does not speak QUIC, instead of running 300 s

## Goal

`curl --http3-only` against a UDP port that answers but not in QUIC fails with the exit code and timing ADR-0144 and curl's ngtcp2 build give (55 after about 10 s), not exit 28 after 300 s.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 1. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`curl --http3-only` against a UDP listener that answers, but not in QUIC, ran 300 s and exited 28; ADR-0144 (HTTP/3 hand-built over a hand-built QUIC, racing TCP as curl's ngtcp2 build does) expects 55 after about 10 s. Likely the QUIC handshake keeps retransmitting the Initial without a handshake deadline (curl's `CURLOPT_CONNECTTIMEOUT` default for QUIC, or ngtcp2's handshake timeout), or the non-QUIC datagrams reset an idle timer. Measure curl 8.22.0's OpenSSL/ngtcp2 build on Linux against such a listener (for example a UDP echo server) for exit code, message and time, and match it on every platform; use an injected `TimeProvider` in the unit test.

## Acceptance criteria

- [ ] The Notes record curl 8.22.0 ngtcp2's exit code, stderr and elapsed time against a non-QUIC UDP listener.
- [ ] The port's `--http3-only` against the same listener gives the same exit code and message within a few seconds of curl's time, pinned by a unit test with a fake clock.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 1.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 11bc3e2d 115dc842 3d836d5e.
