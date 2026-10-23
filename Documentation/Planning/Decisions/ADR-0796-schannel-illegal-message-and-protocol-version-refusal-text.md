# ADR-0796: Schannel's SEC_E_ILLEGAL_MESSAGE text and a TLS 1.2 ceiling's protocol_version refusal

- **Status:** Accepted
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3292
(found by BL-3235's CLI TLS differential cases `tls12-server-tls13only`, `tls-public-badssl-rc4`,
`tls-public-badssl-dh480` and `tls-max-1.2-vs-1.3`).

## Context

`SslStreamTlsProvider`'s Schannel build named a failed handshake's security status with the
system's text for it. For `SEC_E_ILLEGAL_MESSAGE` (0x80090326) curl's `lib/vtls/schannel.c`
prints its own text instead, and curl 8.22.0 Schannel wrote, in all three alert cases:

    schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.

For `--tls-max 1.2` against a TLS 1.3-only server curl 8.22.0 Schannel wrote
`SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported`, while
`SslStream` over the same Schannel returns `SEC_E_ILLEGAL_MESSAGE` with an
`AuthenticationException` naming the server's `ProtocolVersion` alert (confirmed by
`SslStreamTlsProviderTests` against a TLS 1.3-only `SslStream` server on Windows).

## Decision

- `TlsFailureMessages.SchannelSslConnectError` writes curl's fatal-alert line for every
  `SEC_E_ILLEGAL_MESSAGE`, the line the hand-built Schannel path already writes (ADR-0140).
- `SslStreamTlsProvider` passes its `--tls-max` ceiling; with a TLS 1.2 ceiling, an
  `SEC_E_ILLEGAL_MESSAGE` whose `AuthenticationException` names a `ProtocolVersion` alert is
  written as curl's measured `SEC_E_UNSUPPORTED_FUNCTION` line. A ceiling of TLS 1.0 or 1.1
  keeps ADR-0138's `failed to receive handshake`; no ceiling or TLS 1.3 keeps the fatal-alert
  line, as measured for `tls12-server-tls13only` (`--tlsv1.3` against a TLS 1.2 server).
- The hand-built provider is unchanged: it is not the route for these cases, and no case was
  measured through it.

## Consequences

The rule reads `SslStream`'s English alert wording (`TLS alert: 'ProtocolVersion'`); .NET
ships no satellite resources under Native AOT, so the wording is stable. A protocol_version
refusal under a TLS 1.2 ceiling from a server that does offer TLS 1.2 is not measured and
gets the same line.
