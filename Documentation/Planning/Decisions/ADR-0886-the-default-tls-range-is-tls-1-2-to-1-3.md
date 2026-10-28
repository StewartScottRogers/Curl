# ADR-0886: With no TLS version option the handshake offers TLS 1.2 and TLS 1.3 only

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3533
- Decided by Claude under Stewart's delegation.
- Amends ADR-0138's default range.

## Context

ADR-0138 (BL-502) left the choice to the operating system when no `--tlsv1.x` or `--tls-max`
option is set: `TlsVersionRange.ToSslProtocols` returned `SslProtocols.None`. On Windows that lets
Schannel offer TLS 1.1 and 1.0 in `supported_versions` (`00 2b 00 09 08 03 04 03 03 03 02 03 01`)
and complete a handshake with a TLS 1.0 or 1.1-only server. curl's Schannel build offers only TLS
1.3 and 1.2 (`00 2b 00 05 04 03 04 03 03`) and fails such a server with exit 35
`SEC_E_UNSUPPORTED_FUNCTION` (third-party gap report CGR-004).

## Decision

With no minimum and no ceiling, or only a TLS 1.3 ceiling, `ToSslProtocols` returns
`Tls12 | Tls13`, on every platform: curl's default minimum is TLS 1.2 in every backend, so the
OpenSSL build gains nothing from the system default either. A ceiling below TLS 1.3 with no
minimum still starts the range at TLS 1.0, as ADR-0138 measured.

## Consequences

Measured 2026-10-26 against `openssl s_server -tls1_1` and `-tls1` with Windows' curl 8.21.0
Schannel: both curl and Curl exit 35 with the same text. `Cgr004Tests` pins the range.
