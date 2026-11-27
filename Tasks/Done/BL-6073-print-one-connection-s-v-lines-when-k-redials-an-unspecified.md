---
id: BL-6073
title: Print one connection's -v lines when -k redials an unspecified-chain handshake on the hand-built client
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6066]
touches: [Curl.Networking.UnitLibrary/TcpConnector.HandBuiltRedial.cs, Curl.Networking.UnitLibrary/TcpConnector.TlsHandshake.cs, Curl.Networking.UnitTests/TcpConnectorTests.UnspecifiedChainRedial.cs, Curl.Networking.UnitLibrary/RedialedConnectTransferEvents.cs, Curl.Networking.UnitLibrary/HandBuiltRedialRequiredException.cs, Curl.Networking.UnitTests/RedialedConnectTransferEventsTests.cs, Curl.Networking.UnitTests/Fakes/FakeTlsProvider.cs, Documentation/Planning/Decisions/ADR-1459-under-k-an-sslstream-chain-build-that-throws-is-redialled-on-the-hand-built-client.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6073 — Print one connection's -v lines when -k redials an unspecified-chain handshake on the hand-built client

## Goal

`-v` for a `-k` transfer redialled by ADR-1459 shows the lines of one connection, as OpenSSL curl writes them.

## Context

ADR-1459 (BL-6066): `TcpConnector` repeats the whole connect, so the lines before the handshake
(`Host ... was resolved`, `Trying`, the TLS trust lines) appear twice. Measure OpenSSL curl's `-v`
output for a chain whose name constraint does not decode (ADR-1446) before pinning any line;
`CURL_SSL_BACKEND=openssl` with a multi-TLS curl may do so on Windows.

## Acceptance criteria

- [x] The measured curl output, or why it could not be measured, is recorded under Notes.
- [x] A unit test over fakes, running on every platform, shows each line before the handshake once for a redialled direct `-k` connect.

## Notes

- Measured 2026-11-26: Ubuntu (WSL) curl 8.18.0, OpenSSL 3.5.5, `-sv -k --max-time 10` against
  `Record-CurlExchange.ps1 -Tls -ListenAddress <WSL host address>`: one `Trying`, one
  `ALPN: curl offers h2,http/1.1`, one `SSL Trust: peer verification disabled`, one handshake,
  `SSL certificate verification failed, continuing anyway!`, one `Established connection`, exit 0.
- The ADR-1446 chain itself was not measured: the recorder cannot serve a name constraint that does
  not decode, and the Windows curl first on the path is Schannel only. OpenSSL answers error 1 as
  the result of verifying the one handshake, so curl opens no second connection for it.
- Fix (ADR-1460): `RedialedConnectTransferEvents` holds back what the redialled connect repeats, up
  to the redialled handshake's trust; a redial ending before it writes its held lines.
- `touches` widened (no other task in Doing names them) with `RedialedConnectTransferEvents.cs` and
  its tests, `HandBuiltRedialRequiredException.cs` (now says whose handshake asked),
  `Fakes/FakeTlsProvider.cs` and ADR-1459 (its "shows the lines twice" sentence).
- Gates: `dotnet build` 0 warnings; Networking unit tests 4987 passed, 33 skipped;
  `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 0 failing members. The
  whole-solution fast run had 5 failures, all `InProcessCurl_ConnectionReuseCase_Passes` timing
  out at 20 s under load; the 20 cases passed alone. Filed BL-6075.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 2c09db2b 3d555037.
- 2026-11-26: Doing -> Done. A redialled -k connect shows one connection's -v lines (ADR-1460)
