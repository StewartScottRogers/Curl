# ADR-1363: OpenLDAP StartTLS and simple bind failures follow curl 8.21.0

- **Status:** Accepted
- **Date:** 2026-11-24
- **Task:** BL-5908
- **Decided by Claude under Stewart's delegation**
- **Amends:** ADR-0892

## Context

Surl's port conformance run (finding L-4) reported three `ldap://` StartTLS differences on
Linux. ADR-0892 had been measured against curl 8.18.0. Measured on 2026-11-24 under WSL with
upstream curl 8.21.0 linked against OpenLDAP 2.6.15 (Surl's pinned build), against a `socat`
stand-in sending scripted LDAP replies, and read against `lib/openldap.c`:

| Server answer | `--ssl` | `--ssl-reqd` |
| --- | --- | --- |
| StartTLS refused, `protocolError` (2) | binds as LDAPv2, in clear | exit 1 `Unsupported protocol` |
| refused, 1, 12, 52 or 53 | binds as LDAPv3 (measured for 52) | exit 64 `Requested SSL level failed` |
| refused, `invalidCredentials` (49) | binds as LDAPv3 (source) | exit 67 `Login denied` |
| refused, `insufficientAccessRights` (50) | binds as LDAPv3 (source) | exit 9 `Access denied to remote resource` |
| closes instead of answering | exit 7 `LDAP local: connecting ldap_result Can't contact LDAP server` | the same |
| accepts, handshake fails | exit 64, the TLS message | the same |

Every refused `--ssl-reqd` sends an UnbindRequest. A simple LDAPv3 bind answered
`protocolError`, with or without `--ssl`, is sent again as LDAPv2. A refused bind exits 1 for
`protocolError`, 67 for 49, 9 for 50 and 38 otherwise (`oldap_map_error`).

## Decision

The OpenLDAP dialect answers as the table and the paragraph under it say. A SASL bind never
falls back to LDAPv2, as `openldap.c` excludes it. The WinLDAP dialect is unchanged.

ADR-0892's correction, that curl 8.18 exits 1 for result codes 52, 53 and 12, does not hold
for 8.21.0; the port follows 8.21.0, the release it replaces.

Not covered here, and filed as a follow-up task: the plaintext UnbindRequest curl sends after
a failed StartTLS handshake (the TLS provider disposes the connection), the bind curl skips
after StartTLS when there is no `-u`, and a reply to StartTLS that is not an ExtendedResponse,
which keeps the port's earlier behaviour.
