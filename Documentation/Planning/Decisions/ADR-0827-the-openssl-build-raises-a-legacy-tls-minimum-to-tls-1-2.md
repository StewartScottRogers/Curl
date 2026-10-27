# ADR-0827: The OpenSSL build raises a --tlsv1.0 or --tlsv1.1 minimum to TLS 1.2

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3623)
- Relates to: [ADR-0364](ADR-0364-the-tls-1-2-only-hello-carries-each-builds-record-version-and-openssl-refuses-a-legacy-ceiling.md), [ADR-0360](ADR-0360-a-tls-1-0-or-1-1-minimum-runs-on-the-hand-built-client.md)

## Context

CGR-094: on Linux, `curl -sS -k --tlsv1.1 https://localhost:P/index.txt` against
`openssl s_server -WWW -tls1_1 -cipher DEFAULT@SECLEVEL=0` fails in curl, but Curl's hand-built
client offered TLS 1.1 and fetched the file. Measured with curl 8.18.0 on OpenSSL 3.5.5 (WSL),
against servers speaking only TLS 1.1 (`-tls1_1`) and only TLS 1.0 (`-tls1`):

- `--tlsv1.1` and `--tlsv1.0`: exit 35, `TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version`.
  The ClientHello's `supported_versions` offers TLS 1.3 and 1.2 only, so the server alerts.
- `--tlsv1.1 --tls-max 1.2`: exit 35, `error:0A000102:SSL routines::unsupported protocol`; the
  client refuses the server's TLS 1.1 ServerHello.
- `--tlsv1.0 --tls-max 1.1`: exit 35, `no protocols available` (ADR-0364, already matched).
- `--tlsv1.1 --ciphers DEFAULT@SECLEVEL=0` against the TLS 1.1 server: exit 0, `hello tls`.

OpenSSL 3's default security level 1 allows no version below TLS 1.2, whatever minimum curl sets;
only a cipher string's `@SECLEVEL=0` lowers it. curl's Schannel build connects (ADR-0360).

## Decision

`HandBuiltTlsProvider`, in the OpenSSL build, raises a TLS 1.0 or 1.1 `MinimumVersion` to TLS 1.2
when the ceiling is TLS 1.2 or later, unless the last `@SECLEVEL=` command of `--ciphers` is
`@SECLEVEL=0` (OpenSSL applies them in turn). The hello then offers what curl's does, and the
server's alert or ServerHello is reported with the texts `TlsFailureMessages` already has. A legacy
ceiling keeps ADR-0364's refusal before any hello, `@SECLEVEL=0` or not: that combination is left
to a follow-up task. The Schannel build is unchanged.

## Consequences

- `Cgr094Tests` pins the hello, the alert, the refused TLS 1.1 ServerHello and the `@SECLEVEL=0` path.
- `HandBuiltTlsProviderTests.LegacyVersions` no longer expects the OpenSSL build to reach a TLS 1.0
  or 1.1 server with a legacy minimum alone.
