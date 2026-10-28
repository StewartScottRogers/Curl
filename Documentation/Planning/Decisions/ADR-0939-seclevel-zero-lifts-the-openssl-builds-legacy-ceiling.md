# ADR-0939: `@SECLEVEL=0` lifts the OpenSSL build's refusal of a legacy `--tls-max`

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3879)
- Relates to: [ADR-0364](ADR-0364-the-tls-1-2-only-hello-carries-each-builds-record-version-and-openssl-refuses-a-legacy-ceiling.md), [ADR-0827](ADR-0827-the-openssl-build-raises-a-legacy-tls-minimum-to-tls-1-2.md)

## Context

ADR-0364 makes `HandBuiltTlsProvider` in the OpenSSL build refuse a `--tls-max 1.0` or `1.1` ceiling
before any hello (`RefusesItsVersionRange`), as OpenSSL 3's default security level allows no version
below TLS 1.2. ADR-0827 found that `--ciphers` ending at `@SECLEVEL=0` lifts that level for a legacy
minimum, and left the ceiling as it was. Measured on 2026-10-28 with Ubuntu's curl 8.18.0 (OpenSSL
3.5.5) against `openssl s_server -WWW -cipher DEFAULT@SECLEVEL=0`, running
`curl -sS -k --tlsv1.0 --tls-max <max> [--ciphers DEFAULT@SECLEVEL=0] https://localhost:48443/f.txt`:

| Server | `--tls-max` | With `--ciphers DEFAULT@SECLEVEL=0` | Without |
| --- | --- | --- | --- |
| `-tls1_1` | 1.1 | exit 0, the file | exit 35 `no protocols available` |
| `-tls1_1` | 1.0 | exit 35 `error:0A00042E:SSL routines::tlsv1 alert protocol version` | exit 35 `no protocols available` |
| `-tls1` | 1.1 | exit 0, the file | exit 35 `no protocols available` |
| `-tls1` | 1.0 | exit 0, the file | exit 35 `no protocols available` |

## Decision

`RefusesItsVersionRange` holds only when `--ciphers` does not end at `@SECLEVEL=0`, read by the same
`LowersSecurityLevelToZero` ADR-0827's minimum uses (the last `@SECLEVEL=` command counts). With it,
the OpenSSL build's hand-built client runs its TLS 1.0 or 1.1 handshake as the Schannel build does, and a
server alerting the hello is reported through the usual alert text.

## Consequences

`curl --tls-max 1.1 --ciphers DEFAULT@SECLEVEL=0` reaches a legacy-only server in the OpenSSL build as
curl does; without the command the refusal is unchanged. `HandBuiltTlsProviderTests.LegacyVersions`
pins the four `@SECLEVEL=0` rows; the refusal without it stays pinned by ADR-0364's tests.
