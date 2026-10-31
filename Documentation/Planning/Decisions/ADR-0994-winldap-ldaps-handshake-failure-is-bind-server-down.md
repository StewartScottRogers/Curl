# ADR-0994: A failed handshake on WinLDAP's own ldaps:// connection is the bind's Server Down

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4056, for CGR-289). Extends ADR-0979 and ADR-0992.

## Context

ADR-0979 has WinLDAP's second `ldaps://` connection trust only the Windows certificate store. Measured on
2026-10-29 with curl 8.22.0 (Schannel, WinLDAP) and `Record-CurlExchange.ps1 -Script -Tls
-TlsRootCertificateFile`, `curl -sS --cacert ROOT --ssl-no-revoke ldaps://127.0.0.1:PORT/dc=test`:
curl's own handshake verifies against `--cacert` and sends nothing; WinLDAP's second handshake fails
(the throwaway root is not in the system store), and curl exits 38 with
`curl: (38) LDAP local: bind via ldap_win_bind Server Down`. Curl failed that second handshake with
exit 60 `SEC_E_UNTRUSTED_ROOT`.

## Decision

When WinLDAP's own `ldaps` connection opens no connection, `LdapProtocolHandler` ends the transfer with
the bind failure `Server Down` (`CURLE_LDAP_CANNOT_BIND`, 38), as WinLDAP reports its failed connect from
`ldap_win_bind`. A plaintext second connection's failure is still reported as curl's own, as it was not
measured.

## Consequences

The trusted-`--cacert` repro of CGR-289 matches curl's exit code and stderr. Failures of curl's own first
connection (an untrusted `--cacert`, a name mismatch, revocation) are unchanged and still exit 60, as
curl's do.
