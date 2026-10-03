# ADR-0360 — A TLS 1.0 or 1.1 minimum runs on the hand-built client

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1143.
Amends ADR-0140 (adds a row to its routing table) and extends ADR-0331.

## Context

ADR-0331 (BL-714) put a `--tls-max 1.0` or `1.1` ceiling on the hand-built client, so it
connects to a TLS 1.0 or 1.1 server on every platform. A minimum alone (`--tlsv1.0` or
`--tlsv1.1`, no `--tls-max`) stayed on `SslStream`, which leaves the version choice to the
operating system: OpenSSL 3 at its default security level and a Schannel with TLS 1.0 and
1.1 disabled both refuse those versions, so the transfer fails with exit 35 where curl 8.21.0
with Schannel connects (measured exit 0 for `--tlsv1.0` and `--tlsv1.1` alone against
`openssl s_server -tls1` / `-tls1_1`, BL-714).

Two ways to close the gap:

1. Route every TLS 1.0 or 1.1 minimum to the hand-built client.
2. Keep `SslStream`, and retry on the hand-built client when it fails with a protocol-version
   refusal.

## Decision

Option 1. `TlsClientRouting` gains a row: `MinimumVersion` is `Tls10` or `Tls11` routes to the
hand-built client, whatever the ceiling, with the reason "--tlsv1.0 or --tlsv1.1 lets the
versions reach below TLS 1.2". The hand-built client offers TLS 1.3 down to the minimum, so a
modern server still negotiates TLS 1.3 or 1.2, and a legacy server negotiates its version.

## Consequences

- `--tlsv1.0` / `--tlsv1.1` connect to a TLS 1.0 or 1.1 server on Windows, Linux and macOS, as
  either build, with no operating-system TLS stack involved
  (`HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithALegacyMinimumAndNoCeiling_CompletesATransferWithAServerSpeakingOnlyIt`).
- Against a modern server they negotiate TLS 1.2 or 1.3 as before
  (`..._WithALegacyMinimumAgainstAModernServer_NegotiatesTheServersVersion`), but the
  ClientHello is the hand-built client's, not the operating system's. Only users who asked
  for legacy versions see that change.
- The `--ssl-allow-beast` row's TLS 1.0 condition is now also covered by this row; it stays,
  earlier in the table, so its reason still names `--ssl-allow-beast`.

## Alternatives considered

- **Retry after an `SslStream` refusal.** A failed handshake has consumed the connection, so a
  retry needs a fresh TCP connection from the connector, a second `-v` connect trace, and a
  classification of "protocol-version refusal" that differs by platform (Schannel's
  `SEC_E_ALGORITHM_MISMATCH`, OpenSSL's alert text). More code across more projects for a
  rarely used option, and its output would not match curl's single connect.
