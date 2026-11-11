---
id: BL-4899
title: Stop LDAP --ntlm, --digest and --negotiate from sending the password as a cleartext simple bind
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapAuthBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapAuthMethod.cs, Curl.Original.Protocol.Ldap.UnitLibrary/ILdapLogonTokenSource.cs, Curl.Original.Protocol.Ldap.UnitLibrary/NegotiateLogonTokenSource.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapAuthSchemeBindTests.cs, Curl.Original.Protocol.Ldap.UnitTests/Fakes/FakeLogonTokenSource.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapSaslSecurityLayerTests.cs, Curl.Original.Protocol.Ldap.UnitTests/NegotiateLogonTokenSourceTests.cs]
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
- [x] Unit tests in `Curl.Original.Protocol.Ldap.UnitTests` pin the bind choice for each auth mask.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- 2026-11-11 (lane 2, run 1): Measured real curl 8.22.0 (C:/Windows/System32/curl.exe, Schannel/WinLDAP) with Record-CurlExchange.ps1 -Script against a mock answering searches with an empty SearchResultDone and binds with success. `--ntlm`: supportedCapabilities, then Sicily `NTLM` bind (0x8a). `--negotiate`: the logon bind's caps/mechs/caps reads, then Sicily. `--digest`: caps, mechs, then SASL DIGEST-MD5 (0xa3) with empty credentials. Each tries twice, unbinds, and exits 38 `LDAP local: bind via ldap_win_bind Protocol Error`. `--basic`: simple bind. Decided in ADR-1238.
- Done and committed: `LdapProtocolHandler.WinLdapAuthBindingOf` makes curl's `ldap_win_bind` choice: simple bind only with Basic in the mask, otherwise Negotiate > NTLM > Digest with -u's credentials, or the logged-on user when the mask holds none. The OpenLDAP dialect still sends the simple bind, as curl's OpenLDAP build does. `WinLdapLogonBind` is renamed `WinLdapAuthBind` and takes a `WinLdapAuthMethod` (new enum). `ILdapLogonTokenSource.Start` now takes the `NetworkCredential`. The DIGEST-MD5 challenge response is not computed yet: a challenge fails with Local Error. Tests are in LdapAuthSchemeBindTests.cs. Touches were widened to the token source, the bind class and their tests; no other Doing task names LDAP.
- Environment problem: global.json pins SDK 10.0.401, which is not installed on this machine (only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100), so `dotnet` fails in the repository. I built and tested from %TEMP% (SDK 11 targeting net10.0). Build is clean; 656/657 LDAP fast tests pass. The one failure is `NegotiateLogonTokenSourceTests.Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal` (flags & 0x30 = 0x10). That test's inputs are unchanged by this task (it still passes DefaultNetworkCredentials, as the source did before), so I take it to be the SDK/runtime difference. For the same reason Measure-CodeQuality.ps1 refused to measure.
- Left for the next claim, once the pinned SDK resolves: (1) run the fast tests and Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Ldap.UnitLibrary and fix any gate; (2) build Curl.Console and check criteria 1-2 end to end with Record-CurlExchange.ps1 (the scripts in the measurement above: a `read ber`/`send` pair per message). The DIGEST-MD5 challenge response is filed as BL-4951 (depends on this task); it is not part of this task.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: a3192bff f721d6be c47b4c7f a0b49a29.
- 2026-11-11: Doing -> Backlog. Code committed; left: fast tests, quality gate and end-to-end check, which need the pinned SDK 10.0.401 that is missing on this machine
