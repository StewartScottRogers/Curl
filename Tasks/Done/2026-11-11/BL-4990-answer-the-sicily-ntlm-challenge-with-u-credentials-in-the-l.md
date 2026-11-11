---
id: BL-4990
title: Answer the Sicily NTLM challenge with -u credentials in the LDAP --ntlm and --negotiate bind
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/Curl.Original.Protocol.Ldap.UnitLibrary.csproj, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthenticatedNtlmTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4990 — Answer the Sicily NTLM challenge with -u credentials in the LDAP --ntlm and --negotiate bind

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- Done: LdapAuthenticatedBind answers the Sicily challenge (matchedDN) with an NTLM response from -u credentials (tag 0x8b); refused response, undecodable challenge and oversized user name give exit 38 texts. Tests: LdapAuthenticatedNtlmTests. Build clean, fast tests green, Measure-CodeQuality 0 failing members.
- Added a Curl.Ntlm.UnitLibrary reference (Smb has one too); touches widened to the csproj.
- Uses TimeProvider.System and SystemNtlmRandomSource because LdapProtocolHandler is outside touches. Not measured against real curl (no NTLM LDAP server); flow mirrors WinLdapLogonBind. Tests are platform-neutral.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-11: Doing -> Done. LDAP --ntlm/--negotiate bind answers the Sicily NTLM challenge with -u credentials
