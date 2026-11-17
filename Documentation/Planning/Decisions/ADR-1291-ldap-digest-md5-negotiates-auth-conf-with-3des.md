# ADR-1291: The LDAP DIGEST-MD5 bind negotiates qop=auth-conf with 3des (BL-5376)

Status: Accepted. Date: 2026-11-17. Decided by Claude under Stewart's delegation.

## Context

The outside fitness review (STD-093) found that `curl -u user:pw --digest ldap://...` always answered `qop=auth`, where the Windows curl build answers `qop=auth-conf,cipher=3des` when the server offers it and seals every later PDU. A server offering only `auth-conf` made the port give up.

## Decision

`LdapDigestMd5Bind` chooses `auth-conf` when the challenge offers it together with the `3des` cipher, else `auth`, else gives up. The `auth-conf` response names `cipher=3des` and hashes `auth-conf` and an A2 ending in 32 zeros (RFC 2831 section 2.1.2.1). After a successful bind `LdapDigestMd5SecurityLayer` (an `ILdapLogonAuthentication`, so the existing `LdapSaslSecurityLayer` frames it) seals each PDU as RFC 2831 section 2.3: 3DES-CBC (two-key, each 7 key bytes expanded to a parity byte set) over message, padding and the first 10 bytes of HMAC-MD5, then `0x0001` and the sequence number; the IV chains from the last cipher block. Only `3des` is implemented; `auth-int` is never chosen. Built by hand on the base class library.

## Consequences

The sealing is pinned by round trips against a server-side mirror of the layer and by the digest response computed independently in the test; it was not run against a real OpenLDAP server, which a lane cannot start.
