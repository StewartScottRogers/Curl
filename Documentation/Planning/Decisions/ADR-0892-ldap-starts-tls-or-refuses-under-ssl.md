# ADR-0892: LDAP starts TLS or refuses under --ssl and --ssl-reqd, as each platform's curl does

- **Status:** Accepted
- **Date:** 2026-10-26
- **Task:** BL-3542 (CGR-013)
- **Amended by:** ADR-1363 (refusal codes, closed server, handshake failures, LDAPv2 fallback)
- **Decided by Claude under Stewart's delegation**

## Context

`LdapProtocolHandler` never consulted `--ssl` / `--ssl-reqd` (`ITransferContext.SslLevel`), so
an `ldap://` URL with `--ssl-reqd -u user:password` sent the simple bind, password included, in
clear. Real curl never does:

- curl 8.22.0, Schannel and WinLDAP (Windows), measured on 2026-10-26 with
  `Record-CurlExchange.ps1`: `--ssl-reqd` and `--ftp-ssl-reqd` connect, write
  `LDAP local: trying to establish cleartext connection` under `-v`, send nothing, and exit 4
  with `curl: (4) LDAP local: explicit TLS not supported`. `--ssl` binds in plaintext.
- curl 8.18.0, OpenSSL and OpenLDAP (Linux, measured under WSL on 2026-10-26): `--ssl` and
  `--ssl-reqd` first send the StartTLS ExtendedRequest
  `301d02010177188016312e332e362e312e342e312e313436362e3230303337` in plaintext; the gap report's
  verifier saw the bind, search and unbind then go inside TLS.

## Decision

- The WinLDAP dialect returns exit 4 `LDAP local: explicit TLS not supported` after the
  cleartext line and before any byte when `--ssl-reqd` asks for TLS on `ldap://`; `--ssl` binds
  in plaintext as before.
- The OpenLDAP dialect sends StartTLS (`LdapStartTls`) before the bind under `--ssl` and
  `--ssl-reqd`. When the server accepts, the handshake goes through the injected `ITlsProvider`
  (as SMTP, IMAP and POP3 do) and the session continues on the TLS connection; its failure is
  returned as the provider reported it. When the server refuses (or closes), `--ssl` goes on in
  plaintext and `--ssl-reqd` fails with exit 64 `LDAP local: <libldap text for the result code>`
  (`Can't contact LDAP server` for a close), following `openldap.c`'s StartTLS response handling
  (`CURLE_USE_SSL_FAILED` unless `CURLUSESSL_TRY`).
- `ldaps://` is TLS from the first byte and is untouched by either rule.
- A handler built without a TLS provider (only tests do) fails an accepted StartTLS with exit 64
  rather than ever binding in clear.

## Consequences

Correction (BL-5332, 2026-11-16, decided by Claude under Stewart's delegation): this ADR first inferred exit 64 `LDAP local: <libldap text>` for a refused StartTLS from curl's source because the 2026-10-26 `nc` stand-in never got an answer. An outside fitness review (STD-082) measured curl 8.18 (OpenSSL/OpenLDAP) against a stand-in answering with resultCode 2, 52, 53 and 12: exit 1 `curl: (1) Unsupported protocol`, then an UnbindRequest. The port follows the measurement; the closed-server case was not re-measured and keeps exit 64. The OpenLDAP dialect writes no extra `-vlines for StartTLS.
