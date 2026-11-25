# ADR-1362: The OpenLDAP SASL bind runs the shared SASL authenticator

- **Status:** Accepted
- **Date:** 2026-11-24
- **Decided by:** Decided by Claude under Stewart's delegation (BL-5907)
- **Extends:** ADR-1299, ADR-1343

## Context

ADR-1299 made the OpenLDAP build bind with SASL when the auth mask or `--login-options AUTH=` ask for it, with
`DIGEST-MD5` and later `PLAIN` only. Upstream's OpenLDAP build binds with every mechanism of curl's own SASL
layer. Measured with curl 8.18.0 (OpenLDAP 2.6.10, WSL Ubuntu) against `Record-CurlExchange.ps1 -Script`.

## Decision

- The LDAP handler takes an `ISaslAuthenticator`, the one IMAP, POP3 and SMTP take; the engine gives it. It picks
  the mechanism in curl's order and runs every one but `DIGEST-MD5`, which keeps the LDAP library's own exchange
  (`qop=auth`, ADR-1292 and BL-5525). A handler built without one binds with `DIGEST-MD5` or `PLAIN` only.
- Allowed mechanisms are those `AUTH=` names (`*` is all but `EXTERNAL`), else those of the auth mask: Basic is
  `PLAIN` and `LOGIN`, Digest `DIGEST-MD5`, NTLM `NTLM`, Bearer `OAUTHBEARER` and `XOAUTH2`, Negotiate `GSSAPI`.
- SASL is chosen with or without a user. Without one only `EXTERNAL` starts; anything else is exit 67
  `Login denied` after the rootDSE search, with no bind.
- The rootDSE search for `supportedSASLMechanisms` sets time limit 0 (WinLDAP's sets 120).
- The first bind carries the initial response (`PLAIN`, `LOGIN` user name, `EXTERNAL`, the bearer message, NTLM
  type 1, the GSSAPI token); `CRAM-MD5` and `DIGEST-MD5` send no credentials field.
- A challenge that `CRAM-MD5`, `NTLM` or `GSSAPI` cannot answer is cancelled with a bind naming the empty
  mechanism; the next allowed mechanism is tried, else exit 67 `Authentication cancelled`.
- A challenge without `serverSaslCreds` for those three is exit 8. A success before the last answer, or a
  challenge after it, is exit 67 `Login denied`. A GSSAPI context that cannot start is exit 94.

## Consequences

`GSSAPI` binds negotiate no security layer, as curl's SASL does. `GSSAPI` and `DIGEST-MD5` exchanges were not
measured against a live KDC or server for this decision; `GSSAPI` follows RFC 4752 as the shared authenticator
runs it for the mail protocols.
