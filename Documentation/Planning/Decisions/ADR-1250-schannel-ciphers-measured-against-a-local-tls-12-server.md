# ADR-1250 — Schannel --ciphers CALG_AES_128 and CALG_RSA_KEYX measured against a local TLS 1.2 server

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4992
- Decided by Claude under Stewart's delegation.

## Context

ADR-1245 filters the suites a Schannel `--ciphers` ALG_ID list offers. Its unit tests pinned the
filter but the live run against a TLS 1.2 server was unmeasured (BL-4964).

## Measurement

Stand-in: a C# file-based app (RSA 2048 self-signed certificate, `SslStream`, TLS 1.2 only) that
prints `NegotiatedCipherSuite`. Windows 11, curl 8.22.0 Schannel (REF) and Curl, each run with `-k`:

| `--ciphers` | REF exit, suite | Curl exit, suite |
| --- | --- | --- |
| `CALG_AES_128` | 0, `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256` | 0, `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256` |
| `CALG_RSA_KEYX` | 0, `TLS_RSA_WITH_AES_256_GCM_SHA384` | 0, `TLS_RSA_WITH_AES_256_GCM_SHA384` |

`Record-CurlExchange.ps1 -Tls` runs of both lists give exit 0 and the same stdout and request bytes.
The verbose stderr differs in one line: REF prints `* schannel: WARNING: This version of Schannel
negotiates a less-secure TLS version than TLS 1.3 because the user set an algorithm cipher list.`
after `schannel: disabled automatic use of client certificate`; Curl does not (filed as BL-4993).

## Decision

The filter of ADR-1245 stands: it matches REF's negotiated suite for both lists. The exit 35
`CALG_RSA_KEYX` case needs a server that refuses RSA key exchange; `SslStream` on Windows cannot
restrict a server's suites, so that case rests on the ADR-1245 measurement and the hand-built
client's fatal-alert line, and is not re-measured here.
