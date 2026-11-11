---
id: BL-4967
title: Add the LDAP DIGEST-MD5 SASL bind request
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapDigestMd5BindTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4967 — Add the LDAP DIGEST-MD5 SASL bind request

## Goal

The LDAP library can build the DIGEST-MD5 SASL BindRequest (auth tag 0xa3, mechanism DIGEST-MD5) and answer its challenge without sending the password.

## Context

Fitness review STD-001 (2026-11-11): LDAP --ntlm, --digest and --negotiate send the password as a cleartext simple bind (tag 0x80). REF curl 8.22.0 (Schannel) ldap_win_bind: simple bind only when Basic is allowed; NTLM/Negotiate bind name NTLM tag 0x8a with NTLMSSP payload; Digest binds tag 0xa3 DIGEST-MD5 (SASL). Mock stand-ins are PowerShell or C# file-based apps, never Python. Parent task: BL-4899. Reuse LdapBerWriter / LdapRequests; this task adds the request builder only, without wiring it into the handler. Hand-build MD5 digest-response from the BCL.

## Acceptance criteria

- [ ] A new LdapDigestMd5Bind builds an initial SASL BindRequest with tag 0xa3 and mechanism DIGEST-MD5, pinned by byte-level tests in LdapDigestMd5BindTests.
- [ ] It computes the digest-response from a given challenge per RFC 2831 (tested against a known vector) and never sends the password in cleartext.
- [ ] dotnet build is clean and the fast tests (dotnet test --filter "TestCategory!=Integration") pass, with the new type at 100% line and branch coverage.

## Notes

Split from BL-4899 (over its cost cap). BL-4899 had no stash; its earlier work is on branch factory/BL-4899-lane-2-20261111-082724 (it failed to build after a rebase), which may be mined with git cherry-pick --no-commit.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 1bb3a52f b935111b 8c49f348.
