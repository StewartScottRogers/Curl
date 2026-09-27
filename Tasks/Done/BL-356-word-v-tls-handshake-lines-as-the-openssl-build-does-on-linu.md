---
id: BL-356
title: Word -v TLS handshake lines as the OpenSSL build does on Linux and macOS
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-228]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-356 — Word -v TLS handshake lines as the OpenSSL build does on Linux and macOS

## Goal

On Linux and macOS, `VerboseTransferEventWriter` renders a `TlsHandshakeEvent` as curl's OpenSSL build does for `-v`: the protocol version and cipher, the ALPN lines and the server certificate fields, as measured.

## Context

- BL-228 added `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, which words TLS facts only as curl 8.21.0's Schannel build does (the two ALPN lines, measured on Windows). ADR-0009 says Linux and macOS match the OpenSSL build, which ADR-0046 records also prints `* SSL connection using TLSv1.3 / ...`, `* Server certificate:` and its fields.
- Measure a Linux or macOS curl 8.21.0 (OpenSSL) `-v -k` against a loopback TLS server first; never pin unmeasured text. How the writer learns the platform (a constructor argument chosen by `Curl.Console`, or `OperatingSystem.IsWindows()`) is this task's decision; record it in Notes.

## Acceptance criteria

- [x] A test in `Curl.Output.UnitTests` pins the OpenSSL-build `-v` TLS lines for one measured HTTPS exchange, with the command and bytes recorded in Notes; the Schannel tests in `VerboseTransferEventWriterTests` still pass.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Filed from BL-228 (2026-09-27).
- 2026-09-27, lane 1 — measured. curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0
  OpenSSL/3.5.7 (`curlimages/curl:8.21.0` in Docker) against Git for Windows'
  `openssl s_server -accept 28356 -cert cert.pem -key key.pem -www -alpn http/1.1`, a
  self-signed `CN=localhost` RSA-2048 cert (SAN DNS:localhost, IP:127.0.0.1), reached as
  `host.docker.internal`. TLS record lines (`* TLSv1.3 (OUT), ...`, `} [N bytes data]`)
  filtered out; they come from `ReportTlsData`, not the handshake event.
  1. `curl -v -k -o /dev/null https://host.docker.internal:28356/` (info lines in order):
     ```
     * ALPN: curl offers h2,http/1.1
     * SSL Trust: peer verification disabled
     * SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / X25519MLKEM768 / RSASSA-PSS
     * ALPN: server accepted http/1.1
     * Server certificate:
     *   subject: CN=localhost
     *   start date: Sep 27 15:58:54 2026 GMT
     *   expire date: Sep 27 15:58:54 2027 GMT
     *   issuer: CN=localhost
     *   Certificate level 0: Public key type RSA (2048/112 Bits/secBits), signed using sha256WithRSAEncryption
     * OpenSSL verify result: 12
     *  SSL certificate verification failed, continuing anyway!
     * Established connection to host.docker.internal (192.168.65.254 port 28356) from 172.17.0.3 port 40032 
     ```
  2. `--cacert cert.pem --connect-to localhost:28356:host.docker.internal:28356 https://localhost:28356/`
     replaces `SSL Trust: peer verification disabled` with `SSL Trust Anchors:` and
     `  CAfile: /w/cert.pem`, and the verify tail with
     `  subjectAltName: "localhost" matches cert's "localhost"`, `OpenSSL verify result: 0`,
     `SSL certificate verified via OpenSSL.`
  3. `-k --tls-max 1.2 --no-alpn`: no ALPN lines, and
     `SSL connection using TLSv1.2 / ECDHE-RSA-AES256-GCM-SHA384 / x25519 / RSASSA-PSS`
     (OpenSSL's own cipher name, not the IANA name .NET's `TlsCipherSuite` carries).
  Source: `lib/vtls/openssl.c` at `curl-8_21_0` — `Curl_ossl_report_handshake`
  (`%s / %s / %s / %s`, group `[blank]` when OpenSSL has none, signature
  `OBJ_nid2sn`), `ossl_infof_cert`, `infof_certstack` (one line per peer chain level),
  and the verify block (`OpenSSL verify result: %lx`, hex; 12 is
  `X509_V_ERR_DEPTH_ZERO_SELF_SIGNED_CERT`).
- 2026-09-27, lane 1 — back to Backlog. The lines need facts `TlsHandshakeEvent` does
  not carry: the key-exchange group (differs by version: `X25519MLKEM768` on 1.3,
  `x25519` on 1.2, so it cannot be derived), the peer signature type, the OpenSSL verify
  result code, the peer certificate chain beyond the leaf, and the host-name match.
  Plan for the next run: add optional (non-`required`, default `null`) init properties
  to `TlsHandshakeEvent` — `NegotiatedGroupName`, `PeerSignatureTypeName`,
  `CertificateVerifyResult`, `PeerCertificateChain` — and have Output word a missing
  group as `[blank]` and a missing signature type as `UNDEF`, curl's own fallbacks.
  Platform choice: a `VerboseTransferEventWriter` constructor argument naming the TLS
  build's wording, with the existing two-argument constructor choosing it by
  `OperatingSystem.IsWindows()`; the Schannel tests pass the Schannel wording
  explicitly so they hold on Linux CI too. Cipher: a table from `TlsCipherSuite` to
  OpenSSL names for TLS 1.2 (TLS 1.3 names are the same). `SSL Trust` lines are
  written at connect time from the options, not by this event; file that separately.
  Needs an ADR ("Decided by Claude under Stewart's delegation").
  `Curl.Protocol.Abstractions.UnitLibrary` and its tests added to `touches` for this;
  BL-391 (in Doing) touches them, so this waits until it is done.
- 2026-09-27, lane 1 — delivered as planned; decisions in ADR-0083 ("Decided by Claude
  under Stewart's delegation").
  - `TlsHandshakeEvent` gained optional `NegotiatedGroupName`, `PeerSignatureTypeName`,
    `CertificateVerifyResult` (OpenSSL `X509_V_` code) and `PeerCertificateChain`.
  - `TlsBackend` (`Schannel`, `OpenSsl`) is a constructor argument of
    `VerboseTransferEventWriter` and `TraceTransferEventWriter`; their old constructors use
    `PlatformTlsBackend.ForProcess` = `ForPlatform(OperatingSystem.IsWindows())`. The
    Schannel tests now pass `TlsBackend.Schannel` explicitly.
  - New in Output: `OpenSslHandshakeText`, `OpenSslCertificateText`,
    `OpenSslDistinguishedNameText` (port of OpenSSL 3.5 `X509_NAME_print_ex` with curl's
    flags), `OpenSslSecurityBits` (port of `ossl_ifc_ffc_compute_security_bits`).
  - Pinned test: `VerboseTransferEventWriterTests.ReportTlsHandshake_OpenSslSelfSignedExchange_RendersAsCurlsOpenSslBuild`,
    the measured exchange 1 above with the measured certificate copied to
    `Curl.Output.UnitTests/Fixtures/openssl-verbose-localhost.pem`; exchange 3's
    `SSL connection using TLSv1.2 / ECDHE-RSA-AES256-GCM-SHA384 / x25519 / RSASSA-PSS`
    is pinned too. The `SSL Trust` line is not this event's (BL-401).
  - Name printing checked against Git for Windows' OpenSSL 3.5.7:
    `openssl x509 -nameopt oneline,-esc_msb,-space_eq,sep_semi_plus_space` printed
    `C=GB; ST=Some + O=Multi; L=" Leading, and; special \"q\" back #x"; O="#hash<gt>"; ...; OU="trail "`,
    `1.2.3.4=unknown`, `CN=café \01ctl`, and for `LoopbackChain.pem`'s leaf
    `CN=localhost; O=Café Ünïcode; serialNumber=42; title=Dr; UID=u1; L=Salford` and
    `notAfter=Nov  1 05:24:52 2027 GMT`. Cipher names from
    `openssl ciphers -stdname DEFAULT` (PSK and SRP suites left out; .NET cannot
    negotiate them).
  - Defaults taken: unknown key types and curves get no `Certificate level` line; an
    unprintable issuer prints `[NONE]` like the subject; `Documentation/Planning/Decisions`
    added to `touches` for ADR-0083 (no task in Doing names it).
  - Quality: Curl.Output.UnitLibrary 100% line, 100% branch, 0 failing members (worst
    CRAP 10); Curl.Protocol.Abstractions.UnitLibrary 100%/100%, its one failing member
    (`CurlUrlHost.TryNormalize`, complexity 12) predates this task. Tests: Output 335,
    Abstractions 503, whole fast suite green. `dotnet format` reports only the
    repository-wide ENDOFLINE diagnostics that existing files also have.
  - Follow-ups filed: BL-400 (report the event, with these facts, from
    `SslStreamTlsProvider`), BL-401 (TLS record, `SSL Trust`, `subjectAltName` and
    `Proxy certificate:` lines).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Needs Curl.Protocol.Abstractions.UnitLibrary (TlsHandshakeEvent lacks group, signature type, verify code, chain), which BL-391 in Doing touches
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v and the trace dumps word a TLS handshake as curl's OpenSSL build on Linux and macOS: version, cipher, group, certificate block, chain levels and verify result
