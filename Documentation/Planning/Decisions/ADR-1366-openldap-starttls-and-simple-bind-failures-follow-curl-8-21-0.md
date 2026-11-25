# ADR-1366: OpenLDAP StartTLS and simple bind failures follow curl 8.21.0

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

## Added by BL-5931 (2026-11-24, decided by Claude under Stewart's delegation)

Measured the same way, against a `socat` stand-in that also completes or fails the handshake:

| Server | curl 8.21.0, and now the port |
| --- | --- |
| accepts StartTLS, then fails the handshake with an alert | exit 64, then an UnbindRequest (messageID 2) in plaintext |
| accepts StartTLS and the handshake, no `-u` | no BindRequest: the search is messageID 2, the UnbindRequest 3, exit 0 |
| answers StartTLS with a BindResponse | its result code counts as the ExtendedResponse's would: 0 starts the handshake; 52 and 2 act as in the table above |
| answers StartTLS with a SearchResultEntry, `--ssl` | binds as LDAPv3, in clear |
| answers StartTLS with a SearchResultEntry, `--ssl-reqd` | AbandonRequest for message 1, UnbindRequest, exit 64 `Requested SSL level failed` |

- `LdapStartTls` gives the TLS provider a connection whose disposal does nothing, so the
  plaintext connection is still open for the UnbindRequest; the handler disposes it. The
  `ITlsProvider` contract and the providers are unchanged.
- The missing bind applies to a simple bind only: a SASL bind after StartTLS still asks for the
  server's mechanisms, and `--ssl` after a refused StartTLS still binds anonymously
  (`oldap_connecting`).
- Any application-class response with the request's messageID that opens with a result code is
  read as `libldap` reads it. A SearchResultReference counts as a SearchResultEntry does
  (`openldap.c` treats the two alike; read in source).
- Not measured, and unchanged: a reply that is no LDAP response at all fails `--ssl-reqd` with
  exit 64 `LDAP local: Can't contact LDAP server` and sends nothing, and `--ssl` binds as LDAPv3.
