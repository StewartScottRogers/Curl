---
id: BL-450
title: Word TLS messages, SSL data and trust lines in --trace for the OpenSSL build
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-405]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-450 — Word TLS messages, SSL data and trust lines in --trace for the OpenSSL build

## Goal

With `TlsBackend.OpenSsl`, `TraceTransferEventWriter` writes what curl 8.21.0's OpenSSL
build writes to `--trace` and `--trace-ascii` for `ReportTlsMessage`, `ReportTlsData` and
`ReportTlsTrust`: the `== Info:` lines and the `=> Send SSL data` / `<= Recv SSL data` dumps.

## Context

- BL-405 made `VerboseTransferEventWriter` word these for `-v` (`OpenSslMessageText`,
  `OpenSslTrustText`); `TraceTransferEventWriter` still writes nothing for them (its
  `ReportTlsData` is empty and it takes the default `ReportTlsMessage` and `ReportTlsTrust`).
- Measure first: `curl --trace-ascii - -k https://...` with `curlimages/curl:8.21.0` against
  `openssl s_server -www`, as in BL-405's Notes.

## Acceptance criteria

- [x] A test in `Curl.Output.UnitTests` pins the `--trace-ascii` lines of one measured
      TLS 1.3 handshake, recorded in this task's Notes; the Schannel tests still pass.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Output`.

## Notes

- Filed from BL-405 (2026-09-27).
- 2026-09-27, lane 2 — measured. curl 8.21.0 (x86_64-pc-linux-musl) OpenSSL/3.5.7
  (`curlimages/curl:8.21.0`, Docker) against Git for Windows'
  `openssl s_server -accept 28450 -cert cert.pem -key key.pem -www` (self-signed,
  CN=localhost, SAN DNS:localhost, IP:127.0.0.1):
  `curl --trace-ascii - -k -s -o /dev/null https://host.docker.internal:28450/`.
  Handshake part (big dumps elided; bytes after the type byte are random per run):
  ```
  * ALPN: curl offers h2,http/1.1
  => Send SSL data, 5 bytes (0x5)
  0000: .....
  * TLSv1.3 (OUT), TLS handshake, Client hello (1):
  => Send SSL data, 1566 bytes (0x61e)
  0000: ... (25 lines)
  * SSL Trust: peer verification disabled
  <= Recv SSL data, 5 bytes (0x5)
  0000: .....
  * TLSv1.3 (IN), TLS handshake, Server hello (2):
  <= Recv SSL data, 1210 bytes (0x4ba)
  <= Recv SSL data, 5 bytes (0x5)
  * TLSv1.3 (IN), TLS change cipher, Change cipher spec (1):
  <= Recv SSL data, 1 bytes (0x1)
  <= Recv SSL data, 5 bytes (0x5)
  <= Recv SSL data, 1 bytes (0x1)
  * TLSv1.3 (IN), TLS handshake, Encrypted Extensions (8):
  <= Recv SSL data, 6 bytes (0x6)
  <= Recv SSL data, 5 bytes (0x5)        0000: ....G
  <= Recv SSL data, 1 bytes (0x1)
  * TLSv1.3 (IN), TLS handshake, Certificate (11):
  <= Recv SSL data, 822 bytes (0x336)
  <= Recv SSL data, 5 bytes (0x5)
  <= Recv SSL data, 1 bytes (0x1)
  * TLSv1.3 (IN), TLS handshake, CERT verify (15):
  <= Recv SSL data, 264 bytes (0x108)
  <= Recv SSL data, 5 bytes (0x5)        0000: ....E
  <= Recv SSL data, 1 bytes (0x1)
  * TLSv1.3 (IN), TLS handshake, Finished (20):
  <= Recv SSL data, 52 bytes (0x34)
  => Send SSL data, 5 bytes (0x5)
  * TLSv1.3 (OUT), TLS change cipher, Change cipher spec (1):
  => Send SSL data, 1 bytes (0x1)
  => Send SSL data, 5 bytes (0x5)        0000: ....E
  => Send SSL data, 1 bytes (0x1)
  * TLSv1.3 (OUT), TLS handshake, Finished (20):
  => Send SSL data, 52 bytes (0x34)
  * SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS
  ```
  After the body: header, inner type, `* TLSv1.3 (IN), TLS alert, close notify (256):`,
  `<= Recv SSL data, 2 bytes (0x2)`. Info lines are `* ` (not `== Info:`) in 8.21.0.
- Findings: unlike `-v`, the trace has no `traced_data` rule, so every message curl's
  `ossl_trace` reports is dumped — record headers and TLS 1.3 inner content types
  included, though they get no info line. This matches `ossl_trace` (it always calls
  `Curl_debug` with `CURLINFO_SSL_DATA_*`) and `tool_cb_dbg.c` (`=> Send SSL data`,
  `<= Recv SSL data`, same `dump` as body bytes, so `--trace-ascii`'s CR LF rule applies).
- Delivered: `TraceTransferEventWriter` implements `ReportTlsData` (dump, OpenSSL only),
  `ReportTlsMessage` (`OpenSslMessageText` line, then the dump) and `ReportTlsTrust`
  (`OpenSslTrustText` lines); Schannel still writes nothing for any of the three.
  Pinned in `TraceTransferEventWriterOpenSslTlsTests`: record headers, change cipher
  specs, inner types and Encrypted Extensions carry the measured bytes; the random
  handshake bodies carry their measured length and type byte, zero-filled.
  `TraceTransferEventWriterTests.ReportTlsData_WritesNothing` became
  `TlsDataMessagesAndTrust_Schannel_WriteNothing` with an explicit Schannel backend
  (the default constructor's platform backend would be OpenSSL on Linux CI).
- No ADR: a straight port of curl's source with no design choice; ADR-0085's amendment
  is BL-451's. Feature stages run in-session (one small file change); no follow-ups.
- Quality: Curl.Output 100% line, 100% branch, 0 failing of 382 members (worst CRAP 10),
  403 tests; `dotnet build -warnaserror` clean; fast suite green.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --trace and --trace-ascii with the OpenSSL wording write the TLS message lines, SSL data dumps and SSL Trust lines, as measured
