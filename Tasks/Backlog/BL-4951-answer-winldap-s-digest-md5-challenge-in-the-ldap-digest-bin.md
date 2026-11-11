---
id: BL-4951
title: Answer WinLDAP's DIGEST-MD5 challenge in the LDAP --digest bind
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4899]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapAuthBind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthSchemeBindTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4951 — Answer WinLDAP's DIGEST-MD5 challenge in the LDAP --digest bind

## Goal

When the server answers the LDAP `--digest` bind's SASL `DIGEST-MD5` bind with saslBindInProgress and a challenge, the WinLDAP dialect computes the RFC 2831 response from -u's credentials and sends it, as WinLDAP's `LDAP_AUTH_DIGEST` does, instead of failing with Local Error (ADR-1238).
## Context

BL-4899 sends the opening DIGEST-MD5 bind and fails with Local Error on a challenge (`WinLdapAuthBind.DigestBindAsync`). Measure curl 8.22.0 against a mock that sends a DIGEST-MD5 challenge before pinning bytes. The LDAP library may reference only the abstractions, so the response is built here with System.Security.Cryptography.MD5.
## Acceptance criteria

- [ ] Against a mock that sends a DIGEST-MD5 challenge, the second bind carries the response curl 8.22.0 sends (fields measured), and never the password in cleartext.
- [ ] Unit tests in LdapAuthSchemeBindTests pin it; `dotnet build` clean and the fast tests green.
## Notes

## Log

- 2026-11-11: Created.
