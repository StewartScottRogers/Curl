---
id: BL-4899
title: Stop LDAP --ntlm, --digest and --negotiate from sending the password as a cleartext simple bind
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthSchemeBindTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4899 — Stop LDAP --ntlm, --digest and --negotiate from sending the password as a cleartext simple bind

## Goal

LDAP honours the auth-scheme mask like curl's `ldap_win_bind`: a simple bind only when Basic is allowed, NTLM/Negotiate/Digest binds otherwise, so the password never goes out in cleartext when a challenge/response mechanism was asked for.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-001 (High, confirmed: reproduced at least twice).

- Repro: Mock LDAP server on 127.0.0.1:18500 that logs each BindRequest's name and auth choice tag (0x80 simple, 0x8a sicily NTLM, 0xa3 SASL) and answers bind success, one search entry and search done. Run `curl -sS "ldap://127.0.0.1:18500/dc=x" -u "cn=admin,dc=x:pw" --ntlm`, then with `--digest` and `--negotiate`.
- Expected: REF 8.22.0: `--ntlm`/`--negotiate` bind with name `NTLM`, auth tag 0x8a and an `NTLMSSP` payload; `--digest` binds with tag 0xa3 `DIGEST-MD5` (SASL). Exit 38 against this mock (it cannot finish those handshakes). `--basic` or no auth option: simple bind, exit 0.
- Actual: All three send name `cn=admin,dc=x`, tag 0x80, auth `pw` (cleartext simple bind over plain `ldap://`), exit 0.
- Where to start: `LdapProtocolHandler.ExecuteAsync` / `StartTlsAndBindAsync` (~line 278, `bindsAsLogonUser = dialect == LdapDialect.WinLdap && context.Credentials is null`): the auth mask is never consulted and credentials always go to `LdapBind.BindAsync`. `WinLdapLogonBind` / `NegotiateLogonTokenSource` exist and could take explicit credentials.

## Acceptance criteria

- [ ] Against the mock, `--ntlm` sends a bind with tag 0x8a and an `NTLMSSP` payload and never tag 0x80 with `pw`; `--digest` sends tag 0xa3; each exits 38 as REF does.
- [ ] `--basic` and no auth option still send the simple bind (tag 0x80, `pw`) and exit 0.
- [ ] Unit tests in `Curl.Original.Protocol.Ldap.UnitTests` pin the bind choice for each auth mask.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: a3192bff f721d6be c47b4c7f a0b49a29.
