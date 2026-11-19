# ADR-1304: The WinLDAP simple bind over ldaps:// has no LDAPv2 retry (BL-5462)

Status: Accepted. Date: 2026-11-18. Decided by Claude under Stewart's delegation.

## Context

Fitness finding STD-143: against an OpenLDAP server, curl 8.22.0 Schannel on Windows fails an
`ldaps://` simple bind with a wrong password with `curl: (38) LDAP local: bind via ldap_win_bind
Invalid Credentials`, while over plain `ldap://` it retries as LDAPv2 after result 49 and reports
the retry's `Protocol Error`. Curl applied the plain-text retry to `ldaps://` too and printed
`Protocol Error`. The finding was confirmed by two users in two runs; no LDAPS mock server is
available to a lane, so the reference text is taken from the finding.

## Decision

`LdapBind.BindAsync` takes whether the session is `ldaps://`. Over TLS the WinLDAP dialect binds
LDAPv3 once: a refused bind sends an UnbindRequest (as curl's `ldap.c` does on any bind failure)
and fails with exit 38 and WinLDAP's text for the first result code. A missing BindResponse still
fails with `Timeout`. Plain `ldap://` keeps the LDAPv3-then-LDAPv2 sequence of ADR-0166.

## Consequences

`ldaps://` with a wrong password prints `Invalid Credentials`, exit 38, as the reference does;
`ldap://` still prints `Protocol Error` against servers that refuse LDAPv2.
