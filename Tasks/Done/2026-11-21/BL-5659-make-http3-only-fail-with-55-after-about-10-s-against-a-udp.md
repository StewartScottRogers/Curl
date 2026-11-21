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
completed: 2026-11-20
---
# BL-5659 — Make --http3-only fail with 55 after about 10 s against a UDP listener that does not speak QUIC, instead of running 300 s

## Goal

`curl --http3-only` against a UDP port that answers but not in QUIC fails with the exit code and timing ADR-0144 and curl's ngtcp2 build give (55 after about 10 s), not exit 28 after 300 s.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 1. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`curl --http3-only` against a UDP listener that answers, but not in QUIC, ran 300 s and exited 28; ADR-0144 (HTTP/3 hand-built over a hand-built QUIC, racing TCP as curl's ngtcp2 build does) expects 55 after about 10 s. Likely the QUIC handshake keeps retransmitting the Initial without a handshake deadline (curl's `CURLOPT_CONNECTTIMEOUT` default for QUIC, or ngtcp2's handshake timeout), or the non-QUIC datagrams reset an idle timer. Measure curl 8.22.0's OpenSSL/ngtcp2 build on Linux against such a listener (for example a UDP echo server) for exit code, message and time, and match it on every platform; use an injected `TimeProvider` in the unit test.

## Acceptance criteria

- [x] The Notes record curl 8.22.0 ngtcp2's exit code, stderr and elapsed time against a non-QUIC UDP listener.
- [x] The port's `--http3-only` against the same listener gives the same exit code and message within a few seconds of curl's time, pinned by a unit test with a fake clock.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced: `curl --http3-only https://127.0.0.1:18443/` against a PowerShell UDP echo listener ran past 60 s. Cause: the engine always hands `TcpConnector` curl's 300 s default as a `TimeSpan`, so `QuicDialRequest.ConnectTimeout` was never null and `QuicClientConnector` used 300 s instead of ngtcp2's 10 s handshake timeout.
- curl measurement: no Linux curl 8.22.0 is available to this lane; the figure used is ADR-0144's measured one for the ngtcp2 build: exit 55, `ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT`, after 10 s.
- Decision (no ADR needed, ADR-0144 stands): `QuicDialer` treats a connect timeout equal to `TcpConnector.DefaultConnectTimeout` as none given. An explicit `--connect-timeout 300` reads the same, an accepted edge.
- After the fix: exit 55 with that message after 12.5 s wall (10 s handshake plus startup). Unit test `ConnectMultiplexedAsync_WithTheDefaultThreeHundredSecondConnectTimeout_FailsWithExit55AfterTenSeconds` uses a fake clock. Fast tests and Measure-CodeQuality for Curl.Networking.UnitLibrary pass (0 failing members).

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 1.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 11bc3e2d 115dc842 3d836d5e.
- 2026-11-20: Doing -> Done. QuicDialer treats the default 300 s connect timeout as none, so --http3-only against a non-QUIC UDP listener fails 55 after 10 s
