# ADR-0451 — `curl -V` on Windows does not list `HTTP3`

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1825, GF-0032).

## Context

ADR-0144 added `HTTP3` to Curl's `Features:` line on every platform once `--http3` and
`--http3-only` worked over the hand-built QUIC stack. Finding GF-0032 measures `HTTP3` as a
difference on Windows: curl 8.21.0's Schannel reference build does not list it. Measured on this
machine, `curl.exe -V` (curl 8.21.0, x86_64-w64-mingw32, Schannel) prints

`Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`

because that build links no QUIC library (no ngtcp2, quiche or OpenSSL QUIC).

## Decision

1. `CurlVersionText.WindowsFeaturesLine` drops `HTTP3`; `FeaturesLine` (Linux and macOS) keeps it.
2. `--http3`, `--http3-only` and the hand-built HTTP/3 and QUIC stack keep working on every
   platform; only the `Features:` line changes.

## Why

- Matching the platform's curl is the standing rule: a script on Windows that reads `curl -V` must
  see what the Schannel build prints, and a script that checks for `HTTP3` there decides as it
  would with the real binary.
- It follows ADR-0450, which made the same choice for `HTTP2`.

## Consequences

- `features:HTTP3` measures `match` on Windows in the next measurement of the features area.
- This narrows, for Windows only, the `HTTP3` entry ADR-0144 added to ADR-0021.
