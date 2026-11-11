---
id: BL-4968
title: Wire the LDAP bind choice into the handler so --ntlm, --digest and --negotiate never send a cleartext simple bind
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-4965, BL-4966, BL-4967]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthSchemeBindTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4968 — Wire the LDAP bind choice into the handler so --ntlm, --digest and --negotiate never send a cleartext simple bind

## Goal

LDAP --ntlm, --digest and --negotiate bind with NTLM/DIGEST-MD5 instead of a cleartext simple bind, and --basic or no auth option still binds simple.

## Context

Fitness review STD-001 (2026-11-11): LDAP --ntlm, --digest and --negotiate send the password as a cleartext simple bind (tag 0x80). REF curl 8.22.0 (Schannel) ldap_win_bind: simple bind only when Basic is allowed; NTLM/Negotiate bind name NTLM tag 0x8a with NTLMSSP payload; Digest binds tag 0xa3 DIGEST-MD5 (SASL). Mock stand-ins are PowerShell or C# file-based apps, never Python. Parent task: BL-4899. Use LdapBindKindChoice (BL-4965), LdapSicilyBind (BL-4966) and LdapDigestMd5Bind (BL-4967) from StartTlsAndBindAsync.

## Acceptance criteria

- [ ] Against a mock, --ntlm and --negotiate send a bind with tag 0x8a and an NTLMSSP payload and never tag 0x80 with the password; --digest sends tag 0xa3; each exits 38 as REF does.
- [ ] --basic and no auth option still send the simple bind (tag 0x80, password) and exit 0.
- [ ] Unit tests in Curl.Original.Protocol.Ldap.UnitTests (LdapAuthSchemeBindTests) pin the bind choice for each auth mask.
- [ ] dotnet build is clean and the fast tests (dotnet test --filter "TestCategory!=Integration") pass.

## Notes

Split from BL-4899 (over its cost cap). BL-4899 had no stash; its earlier work is on branch factory/BL-4899-lane-2-20261111-082724 (it failed to build after a rebase), which may be mined with git cherry-pick --no-commit.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: a52a37b8 d740cfff c7cd10b9 64c762d4.
