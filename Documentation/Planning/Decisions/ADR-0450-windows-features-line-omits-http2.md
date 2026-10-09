# ADR-0450 — `curl -V` on Windows does not list `HTTP2`

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1824, GF-0031).

## Context

ADR-0141 (Decision 5) added `HTTP2` to Curl's `Features:` line on every platform once `--http2`
was accepted. Finding GF-0031 measures `HTTP2` as a difference on Windows: curl 8.21.0's Schannel
reference build does not list it. Measured on this machine, `C:\Windows\System32\curl.exe -V` prints

`Features: alt-svc AsynchDNS HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz SPNEGO SSL SSPI threadsafe Unicode UnixSockets`

because that build links no nghttp2. ADR-0141 already offers HTTP/2 by default only off Windows.

## Decision

1. `CurlVersionText.WindowsFeaturesLine` drops `HTTP2`; `FeaturesLine` (Linux and macOS) keeps it.
2. `--http2`, `--http2-prior-knowledge` and the hand-built HTTP/2 stack keep working on every
   platform; only the `Features:` line changes.

## Why

- Matching the platform's curl is the standing rule: a script on Windows that reads `curl -V` must
  see what the Schannel build prints, and a script that checks for `HTTP2` there decides as it
  would with the real binary.

## Consequences

- `features:HTTP2` measures `match` on Windows in the next measurement of the features area.
- This narrows, for Windows only, the `HTTP2` entry ADR-0141 added to ADR-0021.
