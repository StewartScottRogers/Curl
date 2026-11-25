# ADR-1343: OpenLDAP SASL bind failures exit 67 "Login denied" or "Authentication cancelled"

- Status: Accepted
- Date: 2026-11-23
- Task: BL-5819
- Decided by Claude under Stewart's delegation.

## Context

The fitness review (STD-171) measured curl 8.18.0 on OpenLDAP: a failed `--digest` SASL bind exits 67 `Login denied` (unknown user, empty user, a server that succeeds without a challenge), and 67 `Authentication cancelled` when the server's `minssf` leaves no mechanism the client can answer. ADR-1302 had the OpenLDAP dialect keep exit 38 `LDAP: cannot bind`, which contradicts that.

## Decision

In the OpenLDAP dialect's SASL bind, a closed connection stays exit 7; an unanswerable challenge (Local Error) is exit 67 `Authentication cancelled`; every other failed reply is exit 67 `Login denied`. The WinLDAP dialect keeps exit 38 and ADR-1302 for it.

## Consequences

`LdapOpenLdapSaslBindTests` pins each case. The simple bind keeps its exit 38 for non-credential failures.
