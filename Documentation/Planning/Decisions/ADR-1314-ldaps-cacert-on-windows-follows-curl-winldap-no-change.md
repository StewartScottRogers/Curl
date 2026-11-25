# ADR-1314: ldaps:// --cacert on Windows already follows curl's WinLDAP, so nothing changes

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5595, Surly report 2026-11-19T1942 finding 1). Builds on ADR-0979, ADR-0992 and ADR-0994.

## Context

The report saw `curl --cacert server.pem ldaps://127.0.0.1:PORT/...` exit 60 `SEC_E_UNTRUSTED_ROOT` on the port
at `b9ca725077`, while the same `--cacert` works for `https://`. It asked whether curl 8.22.0's Schannel build
(WinLDAP) trusts the file.

## Measurement

Measured with curl 8.22.0 (Schannel, WinLDAP) in BL-4056 (ADR-0994) and BL-5461: WinLDAP ignores `--cacert` for
its own handshake and trusts only the Windows certificate store. Without `-u`, curl's own connection verifies
against `--cacert`, then WinLDAP's second connection fails and curl exits 38
`LDAP local: bind via ldap_win_bind Server Down`. With `-u`, curl's own connection carries the bind and a
certificate the store does not trust exits 60 `SEC_E_UNTRUSTED_ROOT`. The OpenSSL build (Linux, macOS) verifies
against `--cacert` itself; this lane runs on Windows only, so that result is not re-measured here.

## Decision

The reference does not trust `--cacert` for ldaps on Windows, so the port keeps ignoring it there (and
`Curl.Api`'s `CertificateAuthorityBlob` likewise): no code changes. The repro's exit 60 came from before
ADR-0994; the port now exits 38 for it, as the reference does, pinned by
`Cgr289Tests.ExecuteAsync_LdapsWinLdapHandshakeFails_FailsWithServerDown`. A certificate trusted by the
Windows store, or `-k` without `-u`, is the way to reach an ldaps server there.

## Consequences

None for behaviour. `--cacert` is honoured for `ldaps://` only by the OpenSSL dialect.
