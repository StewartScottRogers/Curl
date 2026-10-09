# ADR-0452 — `curl -V` on Windows does not list `TLS-SRP`

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1826, GF-0033).

## Context

ADR-0229 and ADR-0328 run TLS-SRP (`--tlsuser`, `--tlspassword`, `--tlsauthtype`) through Curl's
hand-built TLS 1.2 client, as the OpenSSL build does, and Curl's `Features:` line listed `TLS-SRP`
on every platform. Finding GF-0033 measures `TLS-SRP` as a difference on Windows: curl 8.21.0's
Schannel reference build does not list it, because Schannel offers no SRP cipher suites. Measured
on this machine, `curl.exe -V` (Schannel) prints

`Features: alt-svc AsynchDNS HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz SPNEGO SSL SSPI threadsafe Unicode UnixSockets`

with no `TLS-SRP`.

## Decision

1. `CurlVersionText.WindowsFeaturesLine` drops `TLS-SRP`; `FeaturesLine` (Linux and macOS) keeps it.
2. The TLS-SRP options and the hand-built SRP key exchange keep working on every platform; only
   the `Features:` line changes.

## Why

- Matching the platform's curl is the standing rule: a script on Windows that reads `curl -V` must
  see what the Schannel build prints.
- It follows ADR-0450 and ADR-0451, which made the same choice for `HTTP2` and `HTTP3`.

## Consequences

- `features:TLS-SRP` measures `match` on Windows in the next measurement of the features area.
- This narrows, for Windows only, the `TLS-SRP` entry of the `Features:` line.
