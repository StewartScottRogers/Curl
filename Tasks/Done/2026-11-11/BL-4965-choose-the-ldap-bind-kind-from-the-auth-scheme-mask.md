---
id: BL-4965
title: Choose the LDAP bind kind from the auth-scheme mask
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapBindKindChoice.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapBindKindChoiceTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4965 — Choose the LDAP bind kind from the auth-scheme mask

## Goal

A pure function maps the auth-scheme mask to the LDAP bind kind (simple, NTLM sicily, Negotiate, DIGEST-MD5 SASL), simple only when Basic is allowed.

## Context

Fitness review STD-001 (2026-11-11): LDAP --ntlm, --digest and --negotiate send the password as a cleartext simple bind (tag 0x80). REF curl 8.22.0 (Schannel) ldap_win_bind: simple bind only when Basic is allowed; NTLM/Negotiate bind name NTLM tag 0x8a with NTLMSSP payload; Digest binds tag 0xa3 DIGEST-MD5 (SASL). Mock stand-ins are PowerShell or C# file-based apps, never Python. Parent task: BL-4899. Start at LdapProtocolHandler.cs StartTlsAndBindAsync (~line 278); this task only adds the new selector type, without wiring it.

## Acceptance criteria

- [x] A new LdapBindKindChoice in Curl.Original.Protocol.Ldap.UnitLibrary returns the bind kind for each auth mask (none, --basic, --ntlm, --digest, --negotiate, and combinations) as pinned by tests in LdapBindKindChoiceTests.
- [x] A mask without Basic never yields the simple bind kind.
- [x] dotnet build is clean and the fast tests (dotnet test --filter "TestCategory!=Integration") pass, with the new type at 100% line and branch coverage.

## Notes

Split from BL-4899 (over its cost cap). BL-4899 had no stash; its earlier work is on branch factory/BL-4899-lane-2-20261111-082724 (it failed to build after a rebase), which may be mined with git cherry-pick --no-commit.

## Log

- 2026-11-11: Built and tested; quality run: 0 failing members. Decision in ADR-1246. Selector not wired (BL-4968).

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: baa581aa 08c4e9f8 8c49f348.
- 2026-11-11: Doing -> Done. Choose selector done
