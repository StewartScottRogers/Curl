# ADR-0449 — `curl -V` on Windows does not list `GSS-API`

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1823, GF-0030).

## Context

ADR-0142 and ADR-0176 added `GSS-API`, `Kerberos` and `SPNEGO` to Curl's `Features:` line on every
platform once `--negotiate` was answered, and ADR-0171 hand-builds the GSS-API Kerberos initiator.
Gap finding GF-0030 measures `GSS-API` as a gap on Windows: curl 8.21.0's Schannel reference build
does not list it. Measured on this machine, `curl -V` prints

`Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`

because on Windows curl's Negotiate and Kerberos answer through SSPI, not a GSS-API library
(ADR-0142).

## Decision

1. `CurlVersionText.WindowsFeaturesLine` drops `GSS-API`; `FeaturesLine` (Linux and macOS) keeps it.
2. `Kerberos`, `SPNEGO` and `SSPI` stay on the Windows line, as the reference build lists them, and
   `--negotiate`, `--delegation` and `--krb` keep working on every platform; only the `Features:`
   line changes.

## Why

- Matching the platform's curl is the standing rule: a script on Windows that reads `curl -V` must
  see what the Schannel build prints.
- The Windows build's Negotiate is SSPI's, which the line already says with `SSPI`; `GSS-API` there
  would claim a library the Schannel build does not link.

## Consequences

- `features:GSS-API` measures `match` on Windows in the next gap analysis.
- This narrows, for Windows only, the `GSS-API` entry ADR-0142 and ADR-0176 added to ADR-0021.
