# ADR-0340 — The TLS 1.2-only hand-built hello follows the platform curl's measured order

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-941.
Replaces ADR-0235 decision 3 ("below a TLS 1.3 ceiling the profile's lists apply, not its
order").

## Context

Below a TLS 1.3 ceiling `HandBuiltTlsProvider` runs `Tls12ClientConnection`, whose
`Tls12ClientHelloBuilder` sent its extensions in a fixed OpenSSL order. Measured with
`Record-CurlExchange.ps1 -Script` (a `read` then `close`) against `https://localhost`:

- **Schannel** (curl 8.21.0, Windows 11), `--tls-max 1.2`: `server_name`,
  `status_request`, `supported_groups`, `ec_point_formats`, `signature_algorithms`,
  `session_ticket`, ALPN, `extended_master_secret`, `renegotiation_info`. `--tls-max 1.0`
  sends the same without `signature_algorithms`. This is *not* the TLS 1.3 profile's
  order with the TLS 1.3 extensions removed: `supported_groups` and `ec_point_formats` move
  ahead of `signature_algorithms`.
- **OpenSSL** (Ubuntu's curl 8.18.0, OpenSSL 3.5.5, under WSL), `--tls-max 1.2`:
  `renegotiation_info`, `server_name`, `ec_point_formats` (`0,1,2`), `supported_groups`
  (the profile's ECDHE groups), ALPN, `encrypt_then_mac`, `extended_master_secret`,
  `signature_algorithms` — the profile's order without its TLS 1.3 extensions.
  `--tls-max 1.0` sends a `protocol_version` alert and no hello (exit 35).

## Decision

1. `ClientHelloProfile` gains `Tls12ExtensionOrder`, defaulting to `ExtensionOrder`;
   `Schannel` sets the measured order above. The TLS 1.2 builder skips any type it does
   not build, so the OpenSSL default needs no list of its own.
2. `Tls12ClientSettings` gains `ExtensionOrder` (default OpenSSL's, today's order) and
   `FixedExtensions`, sent verbatim in place of the built extension of their type. The
   mapping fixes the profile's `ec_point_formats` (only when an ECDHE group is offered, as
   BL-1094 measured) and its `status_request`; a fixed `status_request` asks for a staple
   without `--cert-status`'s check of it, as in the TLS 1.3 hello.
3. `srp` goes after `server_name`, where OpenSSL sends it; `--cert-status` adds
   `status_request` after `supported_groups` when the order lacks it, as ADR-0235 decision 2.
4. ADR-0235 decision 1 still holds: schemes the client cannot check stay out of
   `signature_algorithms`.

## Consequences

- `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_BelowATls13Ceiling_SendsTheMeasuredExtensionsInTheMeasuredOrder`
  pins each measured hello's extension types, order and data.
- Not covered here: the measured hellos also carry an empty legacy session ID, and
  Schannel's TLS 1.2 record version is `0x0303`; OpenSSL's refusal of `--tls-max 1.0`.

## Alternatives considered

- **The profile's TLS 1.3 order filtered to the TLS 1.2 extensions.** Matches OpenSSL but
  not Schannel, which reorders.
