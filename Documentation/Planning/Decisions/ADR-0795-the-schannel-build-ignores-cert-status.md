# ADR-0795: The Schannel build ignores `--cert-status`

- **Status:** Accepted
- **Date:** 2026-10-23
- **Supersedes in part:** ADR-0191 (the Schannel build's `--cert-status` row)

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3291
(found by BL-3235's CLI TLS differential case `tls-cert-status`).

## Context

ADR-0191 checked the stapled OCSP response under `--cert-status` on every platform, routing the
transfer to the hand-built TLS client, so a server stapling no response failed with exit 91
`No OCSP response received`. The reference on Windows is curl 8.22.0's Schannel build, which has
no certificate-status support. Measured on 2026-10-23 with
`curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`:

| Command | Exit | Output |
| --- | --- | --- |
| `-sS --cacert ca.pem --ssl-no-revoke --cert-status https://<local server, no stapling>/echo` (BL-3235's case) | 0 | `ok` |
| `-sS -v --cert-status -o NUL https://example.com/` | 0 | the same `*` lines as without `--cert-status` |

So the option is silently ignored: no check, no warning, no verbose line.

## Decision

`TlsClientOptionsMapping.Of` sets `TlsClientOptions.RequireCertificateStatus` from
`--cert-status` (`CURLOPT_SSL_VERIFYSTATUS`) only outside the Schannel build
(`RequiresCertificateStatus(verifyStatus, OperatingSystem.IsWindows())`). On Windows the
option therefore no longer routes the transfer to the hand-built client and nothing checks the
status, as curl 8.22.0 Schannel does. The command line still parses and records the option, so
`--libcurl` keeps writing `CURLOPT_SSL_VERIFYSTATUS`. The OpenSSL build keeps ADR-0191 unchanged.
`--doh-cert-status` is not changed here, as it was not measured.

The change sits in the engine mapping, not in `HandBuiltTlsProvider` (as ADR-0793 did for
`--curves`): ignoring the option before routing also keeps the transfer on `SslStream`, which is
the Schannel route.

## Consequences

- `curl --cert-status https://...` on Windows completes as curl 8.22.0 Schannel does.
- `TlsClientOptionsMappingTests` pins each platform's answer under an `OSCondition`.
