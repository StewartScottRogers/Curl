# ADR-1302: The WinLDAP DIGEST-MD5 bind refuses to bind without auth-conf (BL-5434)

Status: Accepted. Date: 2026-11-18. Decided by Claude under Stewart's delegation. Supersedes the `auth` fallback of ADR-1292.

## Context

ADR-1292 had `LdapDigestMd5Bind` answer `qop=auth-conf` when the challenge offers it with a supported cipher, and otherwise fall back to `qop=auth`. The outside fitness review (STD-116) found that this lets a man-in-the-middle strip `auth-conf` from the challenge, or a server offer only `auth`, and the session then runs unprotected and silently. Real curl 8.22.0 (Schannel, Windows, through WinLDAP's `ldap_bind_s` with `LDAP_AUTH_DIGEST`) sends no digest-response in both cases and fails with `curl: (38) LDAP local: bind via ldap_win_bind Operations Error`. The reviewer measured this (3 runs against a server offering only `auth`, 4 against a rewriting relay); a lane cannot start the OpenLDAP container the repro needs, so the text is taken from the finding.

## Decision

In the WinLDAP dialect (the only user of `LdapDigestMd5Bind.AnswerRequest`), the DIGEST-MD5 answer uses `qop=auth-conf` with the strongest supported cipher (3des, then rc4, rc4-56, rc4-40) or nothing: a challenge without `auth-conf` and a supported cipher, or without a nonce, ends the bind before any answer is sent, with LDAP result 1 (`Operations Error`, `WinLdapResultText.OperationsError`), so the transfer fails with exit 38 and the message above. `qop=auth` is never answered.

## Consequences

A downgraded or `auth`-only challenge fails closed as Windows curl does; a server offering `auth-conf` with 3des still binds and seals. A challenge without a nonce now also reports `Operations Error` instead of `Local Error`, the same refusal path. The OpenLDAP dialect (`LdapOpenLdapSaslBind`, after cyrus-sasl, whose default minimum SSF is 0) passes `acceptsQopAuth` and still answers `qop=auth`; an unanswerable challenge there keeps its exit 38 `LDAP: cannot bind`.
