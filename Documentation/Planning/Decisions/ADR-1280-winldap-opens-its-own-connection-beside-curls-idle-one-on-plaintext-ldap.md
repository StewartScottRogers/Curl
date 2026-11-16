# ADR-1280: On plaintext ldap:// WinLDAP opens its own connection beside curl's idle one

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5284). Extends ADR-0978.

## Context

curl 8.21.0's `lib/ldap.c` `ldap_do()` passes the host and port to `ldap_init()`, so WinLDAP opens its
own connection while curl's own stays idle. Surl's Phase 7 measurement saw two connections on a simple or
anonymous bind (connection 0 empty, connection 1 the bind, search and unbind); the port used one. ADR-0978
had already done this for the bind as the logged-on user.

## Decision

The WinLDAP dialect opens curl's own connection, sends nothing on it, and runs the exchange on a second
connection for every plaintext `ldap://` transfer, with or without `-u`. Over `ldaps://` with `-u` it keeps
one connection (BL-5285 measured that path; not re-measured here). The second connection's connect is not
reported to `-v` (WinLDAP opens it, curl does not), so `-v` output is unchanged.

## Consequences

`LdapProtocolHandler.SearchAsync` chooses the two-connection path for `ldap://`. A refused URL on the
Windows build now opens both connections before refusing, as the logon path already did.
