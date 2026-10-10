---
id: BL-1950
title: Match curl when an ECH offer meets a TLS 1.2 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-10
---
# BL-1950 — Match curl when an ECH offer meets a TLS 1.2 server

## Goal

`curl --ech ecl:<config> -k https://<tls-1.2-only server>/` exits as curl's OpenSSL build does, with the same error text.

## Context

Found by BL-1949. Upstream's test4001 offers ECH to a TLS 1.3 server that does not accept it, and curl's OpenSSL build exits 101 ("ECH required"). On macOS the conformance harness's SslStream server can serve only TLS 1.2, and there Curl's hand-built TLS client (`Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs`, `HandshakeTls13OrTls12Async`) falls back to TLS 1.2, completes the handshake, ignores the ECH offer and exits 52 (empty reply). RFC 9849 says a client whose ECH offer meets a server negotiating TLS 1.2 or below treats it as a rejection and aborts with `ech_required`; whether OpenSSL does so, or refuses earlier, must be measured. BL-1949 stopped holding test4001 to the passing list on macOS (`NeedsTls13ServerCases` in `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`).

## Acceptance criteria

- [x] Real curl (OpenSSL build with ECH) measured against a TLS 1.2-only loopback server with `--ech ecl:...`, its exit code and stderr recorded under Notes.
- [x] A test in `Curl.Networking.UnitTests` pins Curl's exit code and error message for an ECH offer answered by a TLS 1.2 ServerHello, matching that measurement.

## Notes

### Measurement (2026-10-10)

curl 8.21.0 on OpenSSL 4.0.0 in the local `curl-ech:8.21.0` Docker image (built for BL-1107), servers
inside the same container, `curl -sS -v -k <args> https://localhost:<port>/`, args `--ech ecl:<list>`,
`--ech true --ech ecl:<list>` and `--ech hard --ech ecl:<list>` (all three behave the same). The list
came from `openssl ech -public_name example.com`. Record-CurlExchange.ps1 cannot reach into the
container, so the servers were `openssl s_server` and two throwaway Perl scripts (a proxy and a canned
ServerHello), as BL-1107 did.

- `openssl s_server -tls1_2`: the hello's `supported_versions` lists TLS 1.3 only; stderr
  `* ECH: ECHConfig from command line`, `TLSv1.3 (OUT), TLS handshake, Client hello (1)`,
  `TLSv1.3 (IN), TLS alert, protocol version (582)`,
  `curl: (35) TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version`. Exit 35.
- `openssl s_server` behind a proxy renaming `supported_versions`: no TLS 1.2 suite offered, so
  `TLS alert, handshake failure (552)`, `curl: (35) TLS connect error: error:0A000410:SSL routines::tls alert handshake failure`.
- A canned TLS 1.2 ServerHello (legacy version 0x0303, no `supported_versions`, suite 0x1301):
  `TLS handshake, Server hello (2)`, `TLSv1.3 (OUT), TLS alert, protocol version (582)`,
  `curl: (35) TLS connect error: error:0A000102:SSL routines::unsupported protocol`. Same with `--tlsv1.3` and no ECH.
- The same ServerHello with suite 0xc02f (never offered): `TLS alert, illegal parameter (559)`,
  `curl: (35) TLS connect error: error:0A000105:SSL routines::wrong cipher returned`.
- `--ech grease` and `--ech true` without a list complete TLS 1.2 against `-tls1_2` (exit 0).

No case is exit 101: with a usable list curl forces a TLS 1.3 minimum (ADR-0359) and never reaches the
ECH check.

### What changed (ADR-0461)

- `HandBuiltTlsProvider.HandshakeAsync` runs the TLS 1.3 client alone when the hello carries an ECH
  configuration, so the hello offers TLS 1.3 only, as curl's does. GREASE keeps the full range.
- `TlsFailureMessages.OpenSslHandBuiltHandshakeFailure` writes `error:0A000102:SSL routines::unsupported protocol`
  for a `protocol_version` alert the client sent.
- Tests: `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithAnEchOfferAnsweredByATls12ServerHello_FailsWithExit35UnsupportedProtocol`
  (modes `true`, `hard`, none; checks the hello's `supported_versions`, the alert record and the text) and a
  `TlsFailureMessagesTests` data row.
- Choice: the Schannel build keeps its own exit 35 text for this case, since curl's Schannel build has no ECH (ADR-0448).
- On macOS test4001 should now end with exit 35, as OpenSSL curl does against a TLS 1.2-only server;
  `NeedsTls13ServerCases` in `Curl.Conformance.UnitTests` is outside this task's `touches` and left as is.
- Coverage: both new conditions are reached both ways by the tests above; Measure-CodeQuality was not run
  (both changed lines are single conditions with each side tested).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-10: Doing -> Done. An ECH offer is a TLS 1.3-only hello; a TLS 1.2 ServerHello to it is exit 35 'unsupported protocol', as curl on OpenSSL 4 does
