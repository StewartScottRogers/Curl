---
id: BL-4913
title: Base64-encode LDAP values that need it, as curl's ldap_value_needs_base64 does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapEntryFormatter.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapValueBase64RuleTests.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapProtocolHandlerTests.Output.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapEntryFormatterTests.cs]
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

- [x] The repro prints exactly `DN: cn=a\n\td1:: YQpi\n\n\td2:: OmNvbG9u\n\n\td3:: PGx0\n\n\td4: plain\n\n`.
- [x] Unit tests pin TAB, CR, leading blank and trailing blank cases.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- 2026-11-11 (lane 9): Measured curl 8.22.0 (Schannel, WinLDAP; the curl.exe on PATH) with `Record-CurlExchange.ps1 -Script` (bind success, one entry, SearchResultDone): `a\nb` -> `:: YQpi`, `:colon` -> `:: OmNvbG9u`, `<lt` -> `:: PGx0`, `a\tb` -> `:: YQli`, `a\rb` -> `:: YQ1i`, `a\vb` -> `:: YQti`, ` lead` -> `:: IGxlYWQ=`, `trail ` -> `:: dHJhaWwg`; `plain`, `a:b<` and the empty value stay plain. Curl now prints the task's repro byte for byte as curl 8.22.0 does (stdout files compared).
- Decision (ADR-1238): `LdapEntryFormatter` applies the 8.22.0 rule to both dialects; the OpenLDAP build could not be measured from Windows (WSL has only curl 8.18.0).
- Widened `touches` to `LdapProtocolHandlerTests.Output.cs` (BL-588's recordings from curl 8.21.0 and 8.18.0 wrote tab, LF, VT, FF and CR raw; their expected text now follows 8.22.0) and `LdapEntryFormatterTests.cs` (its summary described the old rule); no other task in Doing names them.
- Blocker: `global.json` pins SDK 10.0.401 (latestFeature) and this machine has only 10.0.100-rc.1 and 11.x (as BL-4909 found), so `dotnet build` at the root fails "SDK not found" and Measure-CodeQuality.ps1 cannot run. Built and tested from outside the repository with SDK 11.0.100: Curl.Original.Protocol.Ldap.UnitTests and Curl.Console build with 0 warnings; LDAP fast tests 662 of 663 pass. The one failure (`Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal`) is in code this task does not change and is filed as BL-4952. The change adds one trivial predicate (complexity 2), and `LdapValueBase64RuleTests` covers every branch. Once the SDK is back: run `dotnet build`, the fast tests and `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Ldap.UnitLibrary -ReportPath ...`, tick the last criterion and finish. The change is committed on lane 9 (the `fix(ldap)` commit).
- Filed BL-4951: an entry whose last value is empty prints only its DN line (curl 8.22.0 prints `\tdb: \n\n`).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: bce392b4 f5708407 a0b49a29.
- 2026-11-11: Doing -> Blocked. Stewart: the .NET SDK 10.0.4xx that global.json pins is not installed (only 10.0.100-rc.1 and 11.x); please reinstall it so the build, fast tests and quality gate can be run on the committed fix.
