# ADR-0387 — The hand-built OpenSSL build reports a failed handshake before every handshake failure

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1202.
Amends ADR-0371 (BL-1178), whose consequences left `HandBuiltTlsProvider`'s OpenSSL build
reporting a failed `TlsHandshakeEvent` on a pin refusal only.

## Context

ADR-0371's Context has curl 8.18.0's (OpenSSL 3.5.5) `-v` lines: the certificate lines before an
exit 60 for an untrusted root or a host name mismatch, and only the ALPN offer before an exit 35
that negotiated nothing. `SslStreamTlsProvider` prints both since BL-1178.
`HandBuiltTlsProvider`'s OpenSSL build (`--cert-status`, `--curves`, `--ech` and the other ADR-0140
routes) printed nothing of the handshake in either case.

## Decision

`HandBuiltTlsProvider.ReportFailedHandshake` reports a failed event in both builds whenever the
handshake fails, built from `HandBuiltHandshake.NegotiatedBy(verifier.Presented)`: the version,
suite and ALPN answer when the verifier was presented the chain, `SslProtocols.None` when it was
not. `OpenSslHandshakeText` already words both cases.

## Consequences

- The hand-built OpenSSL build's `-v` now prints the certificate lines before an exit 60 and the
  ALPN offer before an exit 35, as `SslStreamTlsProvider` does.
- A `--cert-status` refusal (exit 91, revoked or unknown) is reported as a failed event with what
  was negotiated, ahead of the status line, as curl prints the handshake before `verifystatus`.
- Failures before the ClientHello is sent (`FailBeforeHandshakeAsync`, such as a refused legacy
  version range) still report no event.

## Alternatives considered

- Keeping the pin-refusal-only guard: it left the hand-built routes' `-v` short of curl's for the
  commonest TLS failures.
