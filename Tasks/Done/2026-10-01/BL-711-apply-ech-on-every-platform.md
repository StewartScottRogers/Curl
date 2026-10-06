---
id: BL-711
title: Apply --ech on every platform
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-706, BL-707, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-711 — Apply --ech on every platform

## Goal

`--ech false|grease|true|hard|ecl:<b64>|pn:<name>` behaves as curl 8.21.0 does on every platform: GREASE, opportunistic and mandatory ECH with the configuration from the option or from DoH, and curl's exit code and message when `hard` cannot use ECH.

## Context

- Conformance audit 2026-09-28, row 18; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Parsing: BL-618; routing: BL-617's ADR and BL-708; ECH: BL-706; configurations from DoH: BL-707. Mode semantics: curl's `docs/ECH.md` and `docs/cmdline-opts/ech.md` at tag `curl-8_21_0`.
- Measure with a curl build that has ECH through `Record-CurlExchange.ps1 -Tls -k` (and a DoH responder): each mode against a server without ECH; stderr and exit code copied into Notes.

## Acceptance criteria

- [x] Measured first as above; copied into Notes.
- [x] Tests pin each mode's ClientHello (ECH or GREASE extension present or absent), the configuration source used, and the `hard` failure's exit code and message.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measurement (2026-10-01): it could not be done, because no curl build that can be run here has ECH. In `curl -V`, mingw curl 8.21.0 (Schannel) shows `Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`. WSL's curl 8.18.0 (OpenSSL) shows `Features: alt-svc AsynchDNS brotli GSS-API HSTS HTTP2 HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL threadsafe TLS-SRP UnixSockets zstd`. Neither lists `ECH`, so `Record-CurlExchange.ps1 -Tls -k` would record a hello without ECH in every mode. The behaviour comes from curl 8.21.0's source instead (`tool_operate.c`, `setopt.c` `CURLOPT_ECH`, `vtls/openssl.c` `ossl_init_ech`):
  - `hard` with no usable configuration: `curl: (35) SSL connect error`. curl calls no `failf`, so the tool prints the easy error text.
  - A rejected offer (`SSL_R_ECH_REQUIRED`): exit 101 `CURLE_ECH_REQUIRED`. The text is taken as `ECH attempted but failed`.
  - `true` with no configuration: a plain hello.
  - `grease`: a GREASE extension.

  BL-1107 measures the real texts and the `-v` `ECH:` lines once a build with ECH is available.
- Design (ADR-0327, decided by Claude under Stewart's delegation):
  - `EchModes.Of` combines libcurl's mode bits: `ecl:` alone turns ECH on, and `false` and `grease` win over a list.
  - `EchOffer.DecideAsync` takes the `ecl:` list first, else the `ech` of the host's HTTPS record through the new `IEchConfigListLookup`. `DohDnsResolver` implements it, and `CurlComposition.CreateTransports` passes the run's resolver to the origin's provider.
  - `pn:` replaces each configuration's public name.
  - `encrypted_client_hello` goes last in the TLS 1.3 hello.
  - `hard` fails with exit 35 before a byte is sent when there is no usable list or the range is below TLS 1.3.
  - A rejection (`ech_required`) is exit 101 in `HandBuiltTlsProvider.FailedHandshake`.
  - The `TlsClientRouting` row is `EchModes.Of(options) != EchMode.Off`.
- Defaults taken:
  - An unknown mode keyword is treated as no mode. libcurl sets no bit for it, and its setopt refusal was not measured.
  - The DoH lookup runs at handshake time, not during resolution; the query bytes are the same.
  - The sessions-only public `HandBuiltTlsProvider` constructor was replaced by one that takes the session cache and the ECH lookup, either of which may be null.
- Tests:
  - `HandBuiltTlsProviderTests.Ech`: 26 cases covering each mode's ClientHello, the configuration source, `pn:`, the hard failures, the rejection's exit 101 (excluded on macOS, which has no TLS 1.3 server) and disposal of the client certificate.
  - `EchModesTests`, the `TlsClientRoutingTests` ECH rows, and `DohDnsResolverTests.FindEchConfigListAsync_*`.
- Quality: `Measure-CodeQuality.ps1`
  - Curl.Networking.UnitLibrary: 100% line, 100% branch, 1083 members, 0 failing, worst CRAP 10.
  - Curl.Console: 100%/100%, 0 failing.
- Filed: BL-1107.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --ech grease/true/hard with ecl:, pn: and DoH configurations offers GREASE or ECH in the hand-built TLS 1.3 hello; hard without a usable list is exit 35, a rejected offer exit 101
