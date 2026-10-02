---
id: BL-1149
title: Print the TLS handshake's -v lines before a failed handshake: Schannel's ALPN offer and OpenSSL's certificate details
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-877]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1149 — Print the TLS handshake's -v lines before a failed handshake: Schannel's ALPN offer and OpenSSL's certificate details

## Goal

`curl -v` on a TLS handshake that fails after the ClientHello prints the handshake lines curl prints
before its failure: the Schannel build's `* ALPN: curl offers ...`, and the OpenSSL build's
ALPN offer, `SSL connection using`, certificate details and verify result.

## Context

- Follow-up from BL-877 (ADR-0336). Both TLS providers report a `TlsHandshakeEvent` only for a
  completed handshake, so a failed one prints none of these lines.
- Measured 2026-10-01, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Tls -TlsPublicKeyFile k.pem`,
  `-v -k --pinnedpubkey sha256//AAAA...=`: `* ALPN: curl offers http/1.1` comes right after
  `* schannel: using IP address, SNI is not supported by OS.` and before `*  public key hash:`;
  Curl prints the hash line straight after the `schannel:` line. Measure an exit 60 case too.
- curl 8.18.0's OpenSSL build prints the certificate details before the pin's hash line (BL-608 Notes).
- Code: `SslStreamTlsProvider` and `HandBuiltTlsProvider` failure paths, `TransferEventInfoText`.

## Acceptance criteria

- [x] `-v -k --pinnedpubkey sha256//<wrong>` against `Record-CurlExchange.ps1 -Tls` prints `* ALPN: curl offers http/1.1` before `*  public key hash:` on Windows, pinned by a test.
- [x] The OpenSSL build prints the certificate details before the hash line on a pin refusal, pinned by a test with each build's flag.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-10-02 with `Record-CurlExchange.ps1 -Tls`:
  - curl 8.21.0 Schannel, `-v -k --pinnedpubkey sha256//<wrong>`: `* ALPN: curl offers http/1.1`,
    `*  public key hash: sha256//...`, the mismatch line twice, exit 90. `-v` with the untrusted
    certificate: `* ALPN: curl offers http/1.1` then the `SEC_E_UNTRUSTED_ROOT` line, exit 60 - the
    offer is printed before the ClientHello whatever follows.
  - curl 8.18.0 OpenSSL 3.5.5 (WSL, `-Curl wsl.exe -ListenAddress <host>`), the same pin refusal:
    ALPN offer, TLS message lines, `SSL connection using`, `ALPN: server did not agree ...`,
    `Server certificate:` and details, ` SSL certificate verification failed, continuing anyway!`,
    the hash line, the mismatch line once. Its exit 60 prints the certificate details too
    (left to BL-1178).
- Design (ADR-0363): `TlsHandshakeEvent.Failed`; `TransferEventInfoText` gives a failed Schannel
  handshake only the ALPN offer and hash, the OpenSSL text is unchanged. `SslStreamTlsProvider`
  keeps the negotiation from its certificate callback and reports a failed event always in the
  Schannel build and on a pin refusal in the OpenSSL build; `HandBuiltTlsProvider` reports one in
  the Schannel build only (a failed hand-built handshake keeps no version or suite).
  `ReportPinnedPublicKeyRefusal` skips the hash line when the event carries it.
- The end-to-end run of the built `Curl.Console` against the recorder was skipped: this lane may
  not read `bin/`. Each layer is pinned by its unit tests instead.
- Follow-up filed: BL-1178 (OpenSSL build's lines before exit 60/35, hand-built OpenSSL pin refusal).
- Quality: Curl.Networking.UnitLibrary, Curl.Output.UnitLibrary, Curl.Protocol.Abstractions.UnitLibrary
  100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v prints Schannel's ALPN offer and OpenSSL's certificate details before a failed TLS handshake's pin refusal
