---
id: BL-1178
title: Print the OpenSSL build's handshake -v lines before an exit 60 or 35, and before the hand-built client's pin refusal
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1149]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1178 — Print the OpenSSL build's handshake -v lines before an exit 60 or 35, and before the hand-built client's pin refusal

## Goal

`curl -v` in the OpenSSL build prints the handshake lines curl 8.18.0 prints before an exit 60
(untrusted certificate, host name mismatch) and an exit 35, and the hand-built TLS client's
OpenSSL build prints them before a `--pinnedpubkey` refusal, as `SslStreamTlsProvider` now does.

## Context

- Follow-up from BL-1149 (ADR-0363): a failed handshake is reported as a `TlsHandshakeEvent` with
  `Failed` set. The OpenSSL build reports one only for an `SslStreamTlsProvider` pin refusal.
- Measured 2026-10-02, curl 8.18.0 OpenSSL 3.5.5 in WSL (`Record-CurlExchange.ps1 -Curl wsl.exe
  -ListenAddress <host> -Tls`, `-v`, no `-k`): the ALPN offer, the TLS message lines, `SSL connection
  using`, the ALPN answer, `Server certificate:` and its details, then
  `subjectAltName does not match ipv4 address ...` and `SSL: no alternative certificate subject name
  matches ...`, exit 60. Measure the untrusted-root case (no host mismatch) and an exit 35 too.
- `HandBuiltHandshake.Failed` keeps no version or suite, so the hand-built path needs them from
  `Curl.Tls`' handshake state (`HandBuiltTlsProvider.ReportFailedHandshake`).
- Code: `SslStreamTlsProvider.ReportFailedHandshake`, `HandBuiltTlsProvider.ReportFailedHandshake`,
  `OpenSslHandshakeText`.

## Acceptance criteria

- [ ] The OpenSSL build's `-v` before an exit 60 for a host name mismatch prints the certificate details and the mismatch line as measured, pinned by a test with the OpenSSL build flag.
- [ ] The untrusted-root exit 60 and an exit 35 are measured and pinned the same way.
- [ ] `HandBuiltTlsProvider`'s OpenSSL build reports a failed `TlsHandshakeEvent` with the version, suite and certificate on a pin refusal, pinned by a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
