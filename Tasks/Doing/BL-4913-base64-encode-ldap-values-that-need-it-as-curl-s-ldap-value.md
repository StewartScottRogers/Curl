---
id: BL-4913
title: Base64-encode LDAP values that need it, as curl's ldap_value_needs_base64 does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryFormatter.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapValueBase64RuleTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4913 — Base64-encode LDAP values that need it, as curl's ldap_value_needs_base64 does

## Goal

LDAP output writes a value as `:: base64` when it starts with `:`, `<` or a blank, ends with a blank, or holds any non-ISPRINT byte (TAB, CR, LF included).

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-010 (Medium, confirmed: reproduced at least twice).

- Repro: Mock LDAP server (as in the LDAP bind task) returning entry `cn=a` with values d1=`a\nb`, d2=`:colon`, d3=`<lt`, d4=`plain`. `curl -sS "ldap://127.0.0.1:18500/dc=x" -u "cn=admin,dc=x:pw" | od -c`
- Expected: REF 8.22.0: `DN: cn=a\n\td1:: YQpi\n\n\td2:: OmNvbG9u\n\n\td3:: PGx0\n\n\td4: plain\n\n`. `a\tb` gives `:: YQli`, `a\rb` gives `:: YQ1i`.
- Actual: `DN: cn=a\n\td1: a\nb\n\n\td2: :colon\n\n\td3: <lt\n\n\td4: plain\n\n` - multi-line values break the output and can fake `DN:` lines.
- Where to start: `LdapEntryFormatter.cs`: `IsPrintableOrSpace` accepts 0x09-0x0D (curl's ISPRINT does not); `IsPrintable` / `IsBinary` never check a leading `:` or `<`.

## Acceptance criteria

- [ ] The repro prints exactly `DN: cn=a\n\td1:: YQpi\n\n\td2:: OmNvbG9u\n\n\td3:: PGx0\n\n\td4: plain\n\n`.
- [ ] Unit tests pin TAB, CR, leading blank and trailing blank cases.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: bce392b4 f5708407 a0b49a29.
