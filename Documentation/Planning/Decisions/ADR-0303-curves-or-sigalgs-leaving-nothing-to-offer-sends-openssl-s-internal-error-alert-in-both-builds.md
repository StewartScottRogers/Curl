# ADR-0303 — `--curves` or `--sigalgs` leaving nothing to offer sends OpenSSL's internal_error alert in both builds

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1087.

## Context

ADR-0284 fails a `--curves` or `--sigalgs` value that leaves no group, no signature scheme or
(offering TLS 1.3) no key share with exit 35 and OpenSSL's line, in both builds, because both
apply the options through the hand-built client. Until BL-1087 the hand-built client failed
these before writing a byte. Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 does not: measured
2026-10-01 with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1`, each of
`--curves '*brainpoolP256r1'`, `--curves '*brainpoolP256r1:P-384'`, `--curves '?bogus'` and
`--sigalgs RSA+SHA1` leaves the server holding exactly `15 03 01 00 02 02 50` - a fatal
`internal_error` alert in a record with the ClientHello's legacy version 3.1 - before exit 35.
A refused list (`--curves bogus`, exit 59) writes nothing.

curl 8.21.0's Schannel build ignores both options and sends its ordinary ClientHello, so it
has no answer for this case of its own.

## Decision

When `CurvesAndSignatureAlgorithms.Apply` fails with exit 35, `HandBuiltTlsProvider` writes
the seven measured bytes to the connection and then disposes it, in both builds. A write that
fails is ignored, cancellation apart (which escapes after the connection is disposed, as every
hand-built handshake failure's does): the failure line and exit code stay as they were. An
exit-59 refusal writes nothing.

## Consequences

- The OpenSSL build matches real curl byte for byte on the wire for these failures.
- The Schannel build, whose failure itself follows ADR-0284's OpenSSL model, sends the same
  alert, so one failure looks the same on the wire wherever Curl fails it.

## Alternatives considered

- **No alert in the Schannel build.** Lost: the Schannel build already fails as OpenSSL does
  here (ADR-0284), and real Schannel curl sends a ClientHello, not silence, so silence matches
  neither; one behaviour is simpler and the wire bytes stay consistent with the line printed.
- **Send the alert with the TLS 1.2 record version (`03 03`).** Lost: measured `03 01`.
