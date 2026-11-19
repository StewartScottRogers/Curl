---
id: BL-5521
title: OpenLDAP SASL DIGEST-MD5 bind answers qop=auth only, never auth-conf
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapOpenLdapSaslBindTests.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5521 — OpenLDAP SASL DIGEST-MD5 bind answers qop=auth only, never auth-conf

## Goal

The OpenLDAP dialect's SASL DIGEST-MD5 bind (BL-5435, ADR-1299) answers with `qop=auth` and installs no security layer, as curl's OpenLDAP build does. The WinLDAP dialect keeps `auth-conf`.

## Context

Found by the shift-end review of 2026-11-18 (ADR-0710), pr-review-toolkit code-reviewer (rated Important); confirmed against the code.

**The path:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs:45` calls `LdapAuthenticatedBind.DigestAsync`. That reaches `LdapDigestMd5Bind.ChooseQop` (`LdapDigestMd5Bind.cs:124`), which picks `auth-conf` whenever the server offers it with a supported cipher. This shift widened the supported ciphers to `3des`, `rc4`, `rc4-56` and `rc4-40`.

**Why that is wrong for OpenLDAP:**
- The `auth-conf` path was built to match WinLDAP's SSPI.
- curl's OpenLDAP backend uses curl's own SASL DIGEST-MD5 (`lib/vauth/digest.c`), which only ever sends `qop=auth` and adds no security layer.

**Failure:** `curl --digest -u user:pw ldap://srv/...` in the OpenLDAP dialect, against a server that offers `qop="auth,auth-conf",cipher="rc4"`. Curl answers `qop=auth-conf,cipher=rc4` and wraps every later PDU. Real curl sends `qop=auth` and plain PDUs, so the bytes on the wire differ.

Measure real curl (OpenLDAP build) with `Record-CurlExchange.ps1` if the qop it sends is in doubt.

## Acceptance criteria

- [ ] The OpenLDAP dialect's DIGEST-MD5 response carries `qop=auth`, never `auth-conf`, even when the server offers `auth-conf` with a supported cipher.
- [ ] After an OpenLDAP-dialect DIGEST-MD5 bind, later PDUs are sent unwrapped.
- [ ] The WinLDAP dialect still negotiates `auth-conf`, and its existing tests (`LdapDigestMd5AuthConfTests`) pass unchanged.
- [ ] A unit test in `LdapOpenLdapSaslBindTests.cs` pins `qop=auth` against a challenge offering `auth,auth-conf` with `cipher="rc4"`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created.
