# ADR-0272 — Digest, NTLM and Negotiate Authorization values go beside an -H Authorization header

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-986.

## Context

`HttpRequestHeadFormatter.Format` dropped the authenticator's `Authorization` value whenever an
`-H` value named `Authorization`. curl 8.21.0 (mingw, Schannel, SSPI) was measured on
2026-09-30 with `Record-CurlExchange.ps1` (BL-986 Notes): `--digest -u u:p -H "Authorization: x"`
against a Digest 401 sends `Authorization: x` alone first, then `Authorization: Digest ...`
straight after `Host` and `Authorization: x` in the custom headers' place; `--ntlm -u u:p -H
"Authorization: x"` sends its Type 1 and Type 3 values the same way, each beside
`Authorization: x`. `-u u:p -H "Authorization: x"` sends only `Authorization: x` (BL-954).

libcurl's `output_auth_headers` (lib/http.c) checks the custom headers only before writing
Basic and Bearer, and the `--aws-sigv4` signer bails out when one names `Authorization`;
Negotiate, NTLM and Digest are written unconditionally.

## Decision

The formatter sends a value whose scheme is `Digest`, `NTLM` or `Negotiate` whatever the `-H`
values name, and any other value (Basic, Bearer, an `--aws-sigv4` signature) only when no `-H`
value names `Authorization`. Negotiate follows libcurl's source rather than a measurement: a
loopback server cannot give the Windows build a Kerberos ticket to send, and the code path is
the one NTLM takes.

## Consequences

- The scheme is read from the value's first word, as `HttpAuthUsingLines` already does for
  the `-v` line, so the authenticator's contract is unchanged.
- `Proxy-Authorization` is unaffected: it was already sent beside any `--proxy-header` value.
