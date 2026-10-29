# ADR-0978: The WinLDAP logon bind opens curl's empty connection before WinLDAP's own

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3999, CGR-234)

## Context

CGR-234: without `-u`, curl 8.22.0's Schannel build (`curl -s -S "ldap://127.0.0.1:21001/dc=bench"`)
opens a TCP connection of its own (conn 0) that carries no bytes, and WinLDAP then opens a second
connection (conn 1) for the binds and the search. Curl opened only the second. The gap report's verifier,
re-running on Windows, found that with `-u` (a simple bind) the wire already matched, so the extra
connection is pinned only for the bind as the logged-on user. BL-3763 fixed the bind's failure text.

## Decision

- In the WinLDAP dialect without credentials, `LdapProtocolHandler` connects once for curl's connection,
  keeps it open and silent, connects again to the same target for WinLDAP's connection, binds and
  searches there, and closes both. A failure to open the second connection ends the transfer with that
  connect's own failure.
- With `-u`, and in the OpenLDAP dialect, there is still one connection.
- The verbose connection lines still name curl's connection, the first.

## Consequences

- An LDAP bench on Windows sees the same two connections in the same order as with curl.
- Real curl was not re-measured for this change; the order and the `-u` case come from the gap report's
  Windows re-run (no LDAP bench is in this repository).
