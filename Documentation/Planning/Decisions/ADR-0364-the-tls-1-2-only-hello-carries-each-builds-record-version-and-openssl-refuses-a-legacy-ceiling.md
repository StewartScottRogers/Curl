# ADR-0364 — The TLS 1.2-only hello carries each build's record version, and the OpenSSL build refuses a legacy ceiling

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1152.
Follows ADR-0340 (BL-941), which pinned the extensions of the hello below a TLS 1.3 ceiling.

## Context

Measured 2026-10-02 with `Record-CurlExchange.ps1 -Script` (`read`, `close`):

- curl 8.21.0, Schannel (the Windows reference build), `-k --tls-max 1.2`: the hello is in a
  record of version `0x0303`; with `--tls-max 1.0`, `0x0301`. Both offer an empty legacy session ID.
- Ubuntu's curl 8.18.0, OpenSSL 3.5.5, `-k --tls-max 1.2`: record version `0x0301`, empty
  legacy session ID.
- The same OpenSSL build with `--tls-max 1.0` or `--tls-max 1.1`, with or without `-k` and
  `--tlsv1.0`: no hello; a fatal `protocol_version` alert in a `0x0303` record
  (`15 03 03 00 02 02 46`), the `SSL Trust Anchors` lines, then
  `curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available`.
  OpenSSL 3's default security level allows no version below TLS 1.2.

`HandBuiltTlsProvider` sent every TLS 1.2-only hello in a `0x0301` record, and the OpenSSL build
completed TLS 1.0 and 1.1 handshakes.

## Decision

1. `ClientHelloProfile.Tls12RecordVersionIsTheCeiling` (true for Schannel) and
   `Tls12ClientSettings.ClientHelloRecordVersion` (default TLS 1.0) carry the record version:
   below a TLS 1.3 ceiling the Schannel build writes its first records with the ceiling's
   version, every other case with TLS 1.0. Schannel's `--tls-max 1.1` (`0x0302`) follows the
   same rule; it was not measured, and the rule fits both measured ceilings.
2. The legacy session ID was already empty below TLS 1.3 without a session to resume; a test
   now pins it.
3. The OpenSSL build with a TLS 1.0 or 1.1 ceiling reports the trust, sends the
   `protocol_version` alert record and fails with exit 35 and
   `TlsFailureMessages.OpenSslNoProtocolsAvailable`, before any hello. The Schannel build still
   completes TLS 1.0 and 1.1 handshakes. A `--tlsv1.0` or `--tlsv1.1` minimum with no ceiling is
   unchanged (ADR-0360).

## Consequences

- ADR-0140's legacy-versions row now holds for the Schannel build only; on Linux and macOS
  `--tls-max 1.0` fails as Ubuntu's curl does.
- curl's `-v` `TLSv1.3 (OUT), TLS alert, protocol version (582)` line before the failure is not
  written: the hand-built client writes no TLS message lines yet.
