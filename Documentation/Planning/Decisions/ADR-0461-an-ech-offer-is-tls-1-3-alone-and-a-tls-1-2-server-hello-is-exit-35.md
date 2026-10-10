# ADR-0461 — An ECH offer is TLS 1.3 alone, and a TLS 1.2 ServerHello is exit 35

- **Status:** Accepted
- **Date:** 2026-10-10

Decided by Claude under Stewart's delegation (task BL-1950). Amends ADR-0359.

## Context

ADR-0359 found that curl forces a TLS 1.3 minimum once it tries ECH, and so refuses a usable
`ecl:` list under `--tls-max 1.2`. With the default range, though, `HandBuiltTlsProvider` still
sent one ClientHello offering TLS 1.3 and TLS 1.2 (ADR-0205) with the ECH extension in it. A
TLS 1.2-only server (the conformance harness's `SslStream` server on macOS) then completed a
TLS 1.2 handshake, the ECH offer was ignored, and upstream's test4001 ended with exit 52 where
curl ends with an error.

BL-1950 measured curl 8.21.0 on OpenSSL 4.0.0 (`curl-ech:8.21.0`, the BL-1107 image), `-k`,
with `--ech ecl:<list>`, `--ech true` and `--ech hard` plus the list:

| Server | Curl's ClientHello / server's answer | Result |
| --- | --- | --- |
| `openssl s_server -tls1_2` | `supported_versions` lists TLS 1.3 only; the server alerts `protocol_version` | exit 35, `TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version` |
| `openssl s_server` behind a proxy that hides `supported_versions` | no TLS 1.2 suite offered; the server alerts `handshake_failure` | exit 35, `TLS connect error: error:0A000410:SSL routines::tls alert handshake failure` |
| canned TLS 1.2 ServerHello choosing `TLS_AES_128_GCM_SHA256` | curl sends `protocol_version` | exit 35, `TLS connect error: error:0A000102:SSL routines::unsupported protocol` |
| canned TLS 1.2 ServerHello choosing a suite never offered (`0xc02f`) | curl sends `illegal_parameter` | exit 35, `TLS connect error: error:0A000105:SSL routines::wrong cipher returned` |

`--ech grease` and `--ech true` with no list completed TLS 1.2 handshakes against the same server
(exit 0), so only an offer of a real configuration raises the minimum. No case gives exit 101:
OpenSSL never reaches the ECH check when the version is wrong.

## Decision

1. When `EchOffer` hands the hello a configuration (`ClientSettings.EchConfigs`),
   `HandBuiltTlsProvider.HandshakeAsync` runs `Tls13ClientConnection` alone, so the hello offers
   TLS 1.3 only, its suites and groups too. GREASE and an ECH mode without a usable list keep
   the range as it was.
2. `TlsFailureMessages.OpenSslHandBuiltHandshakeFailure` writes OpenSSL's
   `error:0A000102:SSL routines::unsupported protocol` for a `protocol_version` alert the client
   sent; a received one keeps `tlsv1 alert protocol version`. So a TLS 1.2 ServerHello to an ECH
   offer is exit 35 with the measured text.
3. The Schannel build keeps its own exit 35 text: curl's Schannel build has no ECH (ADR-0448).

## Consequences

- On macOS test4001 now ends with exit 35, as curl's OpenSSL build does against a TLS 1.2-only
  server, not exit 101; whether it returns to the passing list there is the conformance suite's
  question (`NeedsTls13ServerCases`).
- The `wrong cipher returned` case needs a server that picks a suite the client never offered;
  the hand-built client answers it as it answers any such ServerHello and is not pinned here.
