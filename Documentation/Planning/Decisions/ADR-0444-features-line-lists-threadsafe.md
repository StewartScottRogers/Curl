# ADR-0444 — `curl -V` lists `threadsafe` on every platform

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1828, GF-0035).

## Context

ADR-0021 left `threadsafe` off Curl's `Features:` line because it "describes libcurl's global
initialisation being thread-safe; Curl exposes no libcurl API for it to be true of". Gap finding
GF-0035 measures that as a gap: curl 8.21.0's Windows (Schannel) reference build lists it,
measured on 2026-10-08:

```
Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd
```

Upstream sets the feature whenever `curl_global_init` is thread-safe, which every build with
atomics or Windows threads is, so the OpenSSL builds on Linux and macOS list it too.

## Decision

1. `CurlVersionText.FeaturesLine` and `WindowsFeaturesLine` list `threadsafe`, in curl's
   case-insensitive alphabetical order: after `SSL` (and `SSPI` on Windows), before `TLS-SRP`.
2. This supersedes ADR-0021's `threadsafe` rows.

## Why

- Matching the platform's curl is the standing rule, and both reference builds list it.
- The property is true of Curl in curl's sense: Curl's process-wide set-up is the CLR's type
  initialisers and static readonly state, which the runtime runs exactly once under its own lock,
  so starting transfers on several threads (as `--parallel` does) needs no global init call that
  could race. There is no API to expose, but nothing the feature promises is false of Curl.

## Consequences

- A later gap run measures `features:threadsafe` as `match`, and upstream test cases that require
  `threadsafe` are no longer excluded for Curl.
