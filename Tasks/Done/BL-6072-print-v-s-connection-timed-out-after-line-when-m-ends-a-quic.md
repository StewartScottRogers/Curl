---
id: BL-6072
title: Print -v's Connection timed out after line when -m ends a QUIC dial or a UDP proxy tunnel on Linux
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6039]
touches: [Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs, Curl.Networking.UnitLibrary/QuicDialer.cs, Curl.Networking.UnitTests/QuicTimedOutLineTests.cs, Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6072 — Print -v's Connection timed out after line when -m ends a QUIC dial or a UDP proxy tunnel on Linux

## Goal

A QUIC dial or a CONNECT-UDP proxy tunnel that `-m` ends on Linux prints `-v`'s `Connection timed out after N milliseconds` line, as a TCP connect does since BL-6039.

## Context

`TcpConnector.UdpTunnel.cs` and `QuicDialer.cs` report the line only once their own clock reads the limit, so on Linux, where `-m`'s cancellation arrives while the connect's limit still has its start-up time to run, the cancellation escapes without it. ADR-1457 gives the rule `TcpConnector.IsEndedByConnectLimit` applies; apply the same one. Measure real curl with `--http3-only` before pinning text.

## Acceptance criteria

- [x] A unit test pins the line for a QUIC dial cancelled just before its limit and for a UDP proxy tunnel cancelled just before its limit.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-26, curl.se's Windows build 8.18.0 (ngtcp2), `curl --http3-only -sSv -m 3 https://192.0.2.20:1/` (and `--connect-timeout 3`): `Trying`, the trust anchor lines, `* Connection timed out after 3013 milliseconds`, `* closing connection #0`, exit 28. The Ubuntu curl in WSL has no HTTP/3, so Linux was not measured there.
- Applied ADR-1457's rule as it stands, so no new ADR. `TcpConnector.IsAtConnectLimit` and `TimedOutMilliseconds` are now internal static, shared with `QuicDialer`; `QuicDialer` also never names less than the limit (its handshake timer can fire a few ms early on Linux). With no connect timeout, or the default 300 s, a cancelled QUIC dial still escapes.
- Added to `touches`: `TcpConnector.ConnectTimeout.cs` (the shared rule) and the project's `CLAUDE.md`; no task in Doing names either.
- Quality: `Curl.Networking.UnitLibrary` 100% line and branch, 0 failing members. `dotnet build` clean, fast tests green on Windows. The new tests drive the time with fakes, so they are platform-neutral.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 73f0b96d 3d836d5e.
- 2026-11-26: Doing -> Done. QUIC dials and UDP proxy tunnels report -m's timed out line on Linux
