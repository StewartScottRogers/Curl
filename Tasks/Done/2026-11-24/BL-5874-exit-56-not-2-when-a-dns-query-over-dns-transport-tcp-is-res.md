---
id: BL-5874
title: Exit 56, not 2, when a dns:// query over --dns-transport tcp is reset by a server that speaks only TLS
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Tcp, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Tcp]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5874 — Exit 56, not 2, when a dns:// query over --dns-transport tcp is reset by a server that speaks only TLS

## Goal

A `dns://` query over `--dns-transport tcp` whose connection the server resets or closes before a full reply exits 56 (`CURLE_RECV_ERROR`) with a curl-style `Recv failure` message, not 2 (`CURLE_FAILED_INIT`) with .NET's `Unable to read data from the transport connection...` text.

## Context

From Surly cookbook bug report `2026-11-24T1330-Curl-bug-report.md` (batch 14), finding 3 (Low). Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11. Re-run and confirmed the same day.

Repro: `surl -v --cert server.pem --key server.key --dns-transport tls dns://127.0.0.1:18853/` (a port that speaks only TLS); `curl -sS -m 10 --dns-transport tcp dns://127.0.0.1:18853/api.example.test` gives `curl: (2) Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..` Expected 56. Exit 2 means a failed initialisation, which this is not. Start in `Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Tcp/DnsTcpTransport.cs`: map an `IOException`/`SocketException` (reset, or end of stream mid-message) during the read to the receive-failure result the other protocols use, with curl's message shape (`Recv failure: Connection was reset`), and check the UDP, TLS and QUIC transports for the same gap (file follow-ups rather than widen this task if they differ).

## Acceptance criteria

- [x] A test-double TCP connection that resets during the reply makes the `dns://` transfer exit 56 with curl's recv-failure message, pinned by a unit test.
- [x] A connection closed mid-message (short length-prefixed reply) also exits 56, pinned by a unit test.
- [x] No .NET exception text reaches stderr for these cases.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `DnsTcpTransport.ExchangeOverAsync` now maps a failed read to exit 56, worded by `CurlSocketErrorText` (`Recv failure: Connection was reset` on Windows, `strerror`'s words elsewhere), as the other protocol libraries do, so no new ADR.
- Choices: a failed send of the query (the same gap, one method away) exits 55 with `Send failure: ...`; an `IOException` with no socket error inside gets `curl_easy_strerror`'s text (`Failure when receiving data from the peer`, `Failed sending data to the peer`) rather than .NET's; a `MissingCloseNotifyException` keeps its message, which is already curl's. A connection closed mid-message keeps its existing exit 56 and text.
- Other transports: TLS shares `ExchangeOverAsync`, so it is fixed too. HTTPS has the same gap (BL-5903). UDP gives exit 55/56 already but words it differently (BL-5904). QUIC already turns an `IOException` into exit 56.
- Tests are in `Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Tcp/DnsTcpTransportFailureTests.cs`. Fast tests green; `Measure-CodeQuality.ps1` for the library: 100% line and branch, 0 failing members. Non-Windows wording is pinned by `OSCondition` tests that CI runs on Linux and macOS.

## Log

- 2026-11-24: Created from Surly bug report 2026-11-24T1330, finding 3.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 5c1940c5 24a270bb 36a6d25e 3d836d5e.
- 2026-11-24: Doing -> Done. A reset or failed TCP/TLS DNS query exits 56 (read) or 55 (send) with curl's words; follow-ups BL-5903, BL-5904.
