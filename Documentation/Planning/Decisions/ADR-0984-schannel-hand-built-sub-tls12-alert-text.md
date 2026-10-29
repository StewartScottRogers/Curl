# ADR-0984: The Schannel build's text for an alert received under a TLS 1.0 or 1.1 ceiling

- Status: Accepted
- Date: 2026-10-28
- Decided by Claude under Stewart's delegation (BL-4022, CGR-224 TLS-12 (a)(b))

## Context

`HandBuiltTlsProvider` runs every range whose ceiling is TLS 1.0 or 1.1 (ADR-0140). Its Schannel
build printed `schannel: failed to receive handshake, SSL/TLS connection failed` for every failure
of such a range, as curl 8.21.0 did against a server that closed (BL-502). Measured on 2026-10-28
on Windows 11 with curl 8.22.0 (Schannel) against `openssl s_server -WWW` 3.5.9, which received a
ClientHello each time and answered with a `protocol_version` alert, every case was exit 35 with
`curl: (35) schannel: next InitializeSecurityContext failed: ` and:

| server | curl options | status |
|---|---|---|
| default, `-tls1_2` | `--tls-max 1.0`, `--tls-max 1.1`, `--tlsv1.1 --tls-max 1.1` | SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported |
| default | `--tlsv1.0 --tls-max 1.0` | SEC_E_UNSUPPORTED_FUNCTION |
| default, `-tls1_2`, `-tls1_3` | `--tlsv1.0 --tls-max 1.1` | SEC_E_ILLEGAL_MESSAGE (0x80090326), with curl's schannel.c text for a fatal alert |

## Decision

- `TlsFailureMessages.SchannelHandBuiltHandshakeFailure` takes the offered range itself
  (`HandBuiltTlsProvider` passes its options' `MinimumVersion` and `MaximumVersion`), so the whole
  rule lives in the one method its tests drive directly.
- Under a ceiling below TLS 1.2, an alert received is SEC_E_ILLEGAL_MESSAGE's line for that one
  range and SEC_E_UNSUPPORTED_FUNCTION's for every other (one version offered, or the default
  minimum, which Windows 11 does not take down to TLS 1.0). Only `protocol_version` was measured;
  the rule applies to any alert received, the simplest reading of a status Schannel derives from
  the range rather than the alert.
- The transport closing stays `failed to receive handshake` (BL-502's measurement), and so does an
  alert the client itself sent under such a ceiling, which was not measured and keeps its text.
- At or above a TLS 1.2 ceiling nothing changes: any alert is SEC_E_ILLEGAL_MESSAGE's line.

## Consequences

`curl --tls-max 1.0` and `curl --tlsv1.0 --tls-max 1.1` against a server refusing those versions
print what Schannel's curl 8.22.0 prints. `TlsFailureMessagesTests.HandBuilt.cs` pins each case.
