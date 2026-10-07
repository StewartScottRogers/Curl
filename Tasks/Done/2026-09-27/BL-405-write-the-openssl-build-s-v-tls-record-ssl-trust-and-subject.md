---
id: BL-405
title: Write the OpenSSL build's -v TLS record, SSL Trust and subjectAltName lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-356]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-405 — Write the OpenSSL build's -v TLS record, SSL Trust and subjectAltName lines

## Goal

With `TlsBackend.OpenSsl`, `VerboseTransferEventWriter` also writes the lines the OpenSSL
build of curl 8.21.0 prints around a handshake that BL-356 left out: the TLS record lines
(`* TLSv1.3 (OUT), TLS handshake, Client hello (1):` and their `} [N bytes data]`), the
`SSL Trust` lines, the `subjectAltName: "host" matches cert's "host"` line, and
`Proxy certificate:` for a proxy's handshake.

## Context

- ADR-0085 lists these as left out: `ReportTlsData` writes nothing, the handshake event
  carries no host name or proxy flag, and the `SSL Trust` lines come from the connect
  options (`--cacert`, `-k`), not the handshake.
- Measured lines and source references are in BL-356's Notes (`lib/vtls/openssl.c`
  `ossl_verifyhost`, `ossl_trace`; `lib/vtls/vtls.c` for `SSL Trust`). Measure the TLS
  record lines with `Record-CurlExchange.ps1` or Docker `curlimages/curl:8.21.0` first.
- May need new optional properties on `TlsHandshakeEvent` (host name, is-proxy) and a
  record-type argument on `ReportTlsData`; decide in an ADR.

## Acceptance criteria

- [x] Tests in `Curl.Output.UnitTests` pin each of the four kinds of line against a
      measurement recorded in this task's Notes; the Schannel tests still pass.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Output`.

## Notes

- Filed from BL-356 (2026-09-27).
- 2026-09-27, lane 1 — measured. curl 8.21.0 (x86_64-pc-linux-musl) OpenSSL/3.5.7
  (`curlimages/curl:8.21.0`, Docker) against Git for Windows'
  `openssl s_server -accept <port> -cert <c> -key <k> -www`, reached as
  `host.docker.internal`; certs made with `openssl req -x509 -newkey rsa:2048`: `cert.pem`
  (CN=localhost, SAN DNS:localhost, IP:127.0.0.1) on 28405, `cn.pem` (CN=localhost, no SAN)
  on 28406, `w.pem` (CN=wild, SAN DNS:other.test, DNS:*.example.test) on 28407.
  1. `curl -v -k -o /dev/null https://host.docker.internal:28405/` (stderr, handshake part):
     ```
     * ALPN: curl offers h2,http/1.1
     } [5 bytes data]
     * TLSv1.3 (OUT), TLS handshake, Client hello (1):
     } [1566 bytes data]
     * SSL Trust: peer verification disabled
     { [5 bytes data]
     * TLSv1.3 (IN), TLS handshake, Server hello (2):
     { [1210 bytes data]
     * TLSv1.3 (IN), TLS change cipher, Change cipher spec (1):
     { [1 bytes data]
     * TLSv1.3 (IN), TLS handshake, Encrypted Extensions (8):
     { [21 bytes data]
     * TLSv1.3 (IN), TLS handshake, Certificate (11):
     { [822 bytes data]
     * TLSv1.3 (IN), TLS handshake, CERT verify (15):
     { [264 bytes data]
     * TLSv1.3 (IN), TLS handshake, Finished (20):
     { [52 bytes data]
     * TLSv1.3 (OUT), TLS change cipher, Change cipher spec (1):
     } [1 bytes data]
     * TLSv1.3 (OUT), TLS handshake, Finished (20):
     } [52 bytes data]
     * SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS
     ```
     then after the request `{ [5 bytes data]`, two
     `* TLSv1.3 (IN), TLS handshake, Newsession Ticket (4):` / `{ [249 bytes data]`, and at
     the end `* TLSv1.3 (IN), TLS alert, close notify (256):` / `{ [2 bytes data]`. The
     `[5 bytes data]` lines are record headers (no info line); later headers and TLS 1.3
     inner content types are swallowed by curl's `traced_data` rule.
  2. `-k --tls-max 1.2`: `TLSv1.2` lines Client hello 229, Server hello 108, Certificate
     819, Server key exchange (12) 300, Server finished (14) 4, Client key exchange (16) 37,
     change cipher out 1, Finished out 16, Finished in 16.
  3. `--cacert /w/cert.pem --connect-to localhost:28405:host.docker.internal:28405
     https://localhost:28405/`: `SSL Trust Anchors:` / `  CAfile: /w/cert.pem`, and after
     the `Certificate level 0` line `  subjectAltName: "localhost" matches cert's "localhost"`,
     then `OpenSSL verify result: 0` / `SSL certificate verified via OpenSSL.`
     Adding `--capath /w` adds `  CApath: /w` after the CAfile line; no CA option at all
     prints `  CAfile: /cacert.pem` (the image's bundle).
  4. `https://127.0.0.1:28405/`: `  subjectAltName: "127.0.0.1" matches cert's IP address!`
  5. `https://wrong.test:28405/`: ` subjectAltName does not match hostname wrong.test`, then
     the error `SSL: no alternative certificate subject name matches target hostname
     'wrong.test'`; no verify-result lines.
  6. `cn.pem` as `https://localhost:28406/`: ` common name: localhost (matched)`; as
     `https://other:28406/` no info line, only the error `SSL: certificate subject name
     'localhost' does not match target hostname 'other'`.
  7. `w.pem` as `https://A.Example.test:28407/`:
     `  subjectAltName: "A.Example.test" matches cert's "*.example.test"` (host as typed).
  8. `-sv --proxy-insecure -x https://host.docker.internal:28405 http://example.test/`:
     `SSL Trust: peer verification disabled` and `Proxy certificate:` for the proxy's
     handshake, the rest of the block as for a server.
  Source read at `curl-8_21_0`: `lib/vtls/openssl.c` (`ossl_trace`, `tls_rt_type`,
  `ssl_msg_type`, `ossl_populate_x509_store`, `ossl_load_trust_anchors`, `ossl_verifyhost`,
  `ossl_infof_cert`), `lib/vtls/hostcheck.c`; OpenSSL 3.5.0 `ssl/ssl_stat.c`
  (`SSL_alert_desc_string_long`).
- Decisions (Decided by Claude under Stewart's delegation; for ADR-0085, see below):
  - **New facts ride new, optional contract members, so no other project changes.**
    `ITransferEvents` gained two default interface members: `ReportTlsMessage(TlsMessageEvent)`
    (OpenSSL's message callback: version number, `TlsContentType`, direction, bytes; by
    default it forwards the bytes to `ReportTlsData`) and `ReportTlsTrust(TlsTrustEvent)`
    (`VerifiesPeer`, `HasCaCertificateBlob`, `CaCertificateFile`, `CaCertificateDirectory`;
    a no-op by default). `TlsHandshakeEvent` gained `IsProxy` and `VerifiedHostName` (host as
    typed, `null` when the host is not checked). Changing `ReportTlsData`'s signature was
    rejected: it would break the implementations and fakes in `Curl.Networking`, `Curl.Cookies`,
    `Curl.Core` and `Curl.Protocol.Http` tests, all outside this task.
  - **The Schannel wording writes nothing for the new events**, as curl's Schannel build has
    no message callback and no `SSL Trust` lines; the OpenSSL wording writes TLS bytes as
    `{`/`}` data lines under the same `traced_data` rule as body bytes.
  - **Host-name checking is ported, not reported.** `OpenSslHostNameText` re-runs
    `ossl_verifyhost` on the certificate the event carries (SAN of the host's kind, else the
    last single-valued CN; RFC 6125 wildcards as `hostcheck.c`), because the line names the
    matching SAN entry, which `SslStream` never exposes. A failed check writes only curl's
    info line (or none) and stops before the verify result; the error text is the transfer's.
  - Defaults taken: a CN in a multi-valued RDN is skipped (OpenSSL would find it; rare);
    `CURL_CA_FALLBACK`, native CA stores and the "error setting certificate file" variants
    are not worded (not in the Linux build measured); IPv4 host detection counts dots (curl
    uses `inet_pton`).
  - ADR-0085 could not be amended here: `Documentation/Planning/Decisions` is in BL-380's
    `touches` (in Doing). Filed as BL-451 rather than widening this task.
- Delivered: `Curl.Protocol.Abstractions.UnitLibrary` — `TlsContentType`, `TlsMessageEvent`,
  `TlsTrustEvent`, the two `ITransferEvents` defaults, `TlsHandshakeEvent.IsProxy` and
  `.VerifiedHostName`. `Curl.Output.UnitLibrary` — `OpenSslMessageText`, `OpenSslTrustText`,
  `OpenSslHostNameText`; `OpenSslCertificateText.PeerCertificate` (was `ServerCertificate`)
  says `Proxy certificate:`; `OpenSslHandshakeText` adds the host-name line;
  `VerboseTransferEventWriter` writes the lot for `TlsBackend.OpenSsl`.
  Pinned in `VerboseTransferEventWriterOpenSslTlsTests` (measurements 1-8) plus
  `OpenSslMessageTextTests`, `OpenSslTrustTextTests`, `OpenSslHostNameTextTests` (source).
- Quality: Curl.Output 100% line, 100% branch, 0 failing members (worst CRAP 10), 399
  tests; Curl.Protocol.Abstractions 100%/100%, 0 failing, 515 tests; whole fast suite
  green; `dotnet format` shows only the repository-wide ENDOFLINE diagnostics.
- Follow-ups filed: BL-451 (ADR-0085 amendment), BL-452 (Networking reports trust, host
  name and proxy handshakes; ALPN-offer ordering), BL-450 (`--trace` wording).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v with the OpenSSL wording writes the TLS message and data lines, SSL Trust lines, subjectAltName/common name lines and Proxy certificate:, as measured
