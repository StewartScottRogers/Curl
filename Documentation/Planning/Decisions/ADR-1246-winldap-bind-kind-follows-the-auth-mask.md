# ADR-1246 — The WinLDAP bind kind follows the auth mask

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4965
- Decided by Claude under Stewart's delegation.

## Context

The fitness review of 2026-11-11 (STD-001) found that LDAP with `-u` and `--ntlm`, `--digest`
or `--negotiate` sends the password in a cleartext simple bind (tag `0x80`). curl 8.22.0's
Schannel build (`lib/ldap.c`, `ldap_win_bind`) sends a simple bind only when the user, the
password and Basic in the auth mask are all present; otherwise `ldap_win_bind_auth` binds with
`LDAP_AUTH_NEGOTIATE`, `LDAP_AUTH_NTLM` or `LDAP_AUTH_DIGEST`, checked in that order, and with
none of them, or without credentials, as the logged-on user (`LDAP_AUTH_NEGOTIATE` with no
credentials).

## Decision

`LdapBindKindChoice.Choose(HttpAuthSchemes, bool hasCredentials)` returns an `LdapBindKind`:
`LogonUser` without credentials; `Simple` when the mask holds Basic; otherwise `Negotiate`,
`Ntlm` or `Digest`, the first the mask holds in that order, or `LogonUser`. A mask without Basic
never yields `Simple`. The choice is a pure function so it is tested without a network; wiring it
into `LdapProtocolHandler` is separate work (BL-4899's split). The OpenLDAP dialect keeps its
simple bind, as curl's OpenLDAP build does.

## Consequences

The order and the logon-user fallback match curl's source, so `--anyauth` (which holds Basic)
still sends a simple bind, as curl does.
