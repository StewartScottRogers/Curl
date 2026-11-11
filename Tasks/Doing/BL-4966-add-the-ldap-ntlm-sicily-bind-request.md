---
id: BL-4966
title: Add the LDAP NTLM (sicily) bind request
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapSicilyBind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapSicilyBindTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4966 — Add the LDAP NTLM (sicily) bind request

## Goal

The LDAP library can build the NTLM (sicily) BindRequest, auth tag 0x8a, name NTLM, carrying an NTLMSSP negotiate payload.

## Context

Fitness review STD-001 (2026-11-11): LDAP --ntlm, --digest and --negotiate send the password as a cleartext simple bind (tag 0x80). REF curl 8.22.0 (Schannel) ldap_win_bind: simple bind only when Basic is allowed; NTLM/Negotiate bind name NTLM tag 0x8a with NTLMSSP payload; Digest binds tag 0xa3 DIGEST-MD5 (SASL). Mock stand-ins are PowerShell or C# file-based apps, never Python. Parent task: BL-4899. Reuse WinLdapLogonBind / NegotiateLogonTokenSource / LdapBerWriter / LdapRequests where they fit; this task adds the request builder only, without wiring it into the handler.

## Acceptance criteria

- [x] A new LdapSicilyBind builds a BindRequest with name NTLM, context tag 0x8a and an NTLMSSP payload, pinned by byte-level tests in LdapSicilyBindTests.
- [x] The password is never present in the encoded request.
- [x] dotnet build is clean and the fast tests (dotnet test --filter "TestCategory!=Integration") pass, with the new type at 100% line and branch coverage.

## Notes

Split from BL-4899 (over its cost cap). BL-4899 had no stash; its earlier work is on branch factory/BL-4899-lane-2-20261111-082724 (it failed to build after a rebase), which may be mined with git cherry-pick --no-commit.

Notes: LdapSicilyBind.NegotiateRequest validates the NTLMSSP type 1 header and delegates to LdapRequests.SicilyBind (choice 10 = tag 0x8a, name NTLM); it takes no password. Measure-CodeQuality ran every test project and stopped on an unrelated Conformance.Extended failure (RequirementMatrixTests), so the type (one branch, both covered, complexity 2) was not measured further.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: ba49ea96 5baa8242 8c49f348.
