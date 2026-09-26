---
id: BL-060
title: Decide how --ciphers and --tls13-ciphers behave where the BCL cannot set cipher suites
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-060 — Decide how --ciphers and --tls13-ciphers behave where the BCL cannot set cipher suites

## Goal

Stewart decides what `--ciphers` (TLS 1.2 and below) and `--tls13-ciphers` (TLS 1.3) do
in Curl on each platform, given that the BCL cannot restrict cipher suites on Windows.
The decision is recorded as a new ADR under `Documentation/Planning/Decisions/`.

## Context

Upstream (<https://curl.se/docs/manpage.html>, as published for curl 8.23.0 on
2026-09-26): `--ciphers` "Specify which cipher suites to use in the connection if it
negotiates TLS 1.2 (1.1, 1.0)"; `--tls13-ciphers` does the same for TLS 1.3. A cipher
list curl cannot apply is exit 59 `CURLE_SSL_CIPHER`
(<https://curl.se/libcurl/c/libcurl-errors.html>).

Measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32, Schannel,
Release-Date 2026-06-24) against `openssl s_server` on loopback, `-sS -k`:

| Command | Exit | stderr |
| --- | --- | --- |
| `--ciphers BOGUS` | 59 | `curl: (59) schannel: Failed setting algorithm cipher list` |
| `--ciphers ECDHE-RSA-AES128-GCM-SHA256 --tls-max 1.2` (OpenSSL name) | 59 | the same line |
| `--ciphers TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 --tls-max 1.2` (IANA name) | 59 | the same line |
| `--tls13-ciphers TLS_AES_128_GCM_SHA256` | 0 | nothing |

Only those names were tried; which `--ciphers` names the Schannel build accepts, if any,
was not established.

The BCL: `SslClientAuthenticationOptions.CipherSuitesPolicy` takes `TlsCipherSuite`
values (IANA names) and is not supported on Windows, where assigning it makes the
handshake throw `PlatformNotSupportedException`; on Linux and macOS it is honoured.
OpenSSL-style names such as `ECDHE-RSA-AES128-GCM-SHA256` would need a hand-written
name map (no package may be added).

Choices, per platform:

1. Windows: refuse every value with exit 59 (the measured names all fail there anyway);
   Linux/macOS: honour through `CipherSuitesPolicy`, accepting OpenSSL names, IANA names,
   or both.
2. Accept and ignore the options everywhere, with or without a warning line: a
   divergence.
3. Refuse every value everywhere with exit 59: a divergence on Linux/macOS.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/` (next free number), status
      Accepted, states what `--ciphers` and `--tls13-ciphers` do on Windows, Linux and
      macOS: honoured (and which name syntax), ignored (and any warning text), or refused
      (exit 59 and the message text).
- [ ] The ADR names every divergence from upstream curl it accepts.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.

## Notes

**Decision (Stewart, 2026-09-26):** Option 1. Windows: behave as the measured Schannel build of curl 8.21.0 - `--ciphers` refused with exit 59 `schannel: Failed setting algorithm cipher list`; `--tls13-ciphers` follows the measurement above (accepted, exit 0), which the ADR records. Linux and macOS: honoured through `CipherSuitesPolicy`, accepting both OpenSSL names and IANA names (a hand-written name map; no package).

Blocks BL-066. BL-067 parses both options verbatim regardless of this decision.

## Log

- 2026-09-26: Created.
- 2026-09-26: Stewart decided: refuse on Windows as the Schannel build does, honour on Linux/macOS. Reassigned to Claude to record the ADR.
