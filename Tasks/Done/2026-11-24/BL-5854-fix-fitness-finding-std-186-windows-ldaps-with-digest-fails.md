---
id: BL-5854
title: Fix fitness finding STD-186: Windows ldaps:// with --digest fails with exit 38 "Operations Error"; curl binds with DIGEST-MD5 qop=auth over TLS and succeeds
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5854 — Fix fitness finding STD-186: Windows ldaps:// with --digest fails with exit 38 "Operations Error"; curl binds with DIGEST-MD5 qop=auth over TLS and succeeds

## Goal

STD-186 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Windows ldaps:// with --digest fails with exit 38 "Operations Error"; curl binds with DIGEST-MD5 qop=auth over TLS and succeeds. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Medium (an important feature is broken; fails closed) · **Confidence:** Confirmed (3 runs) · **Area:** LDAP DIGEST-MD5 over LDAPS, Windows build.
- **Setup:** osixia/openldap:1.5.0 with ldaps on 127.0.0.1:22636 (self-signed), `olcSaslSecProps: noanonymous,noplain,maxssf=256`, user `alice` / `secret` (`uid=alice,ou=people,dc=example,dc=org`, `cn: Alice Example`). Over TLS the server offers only `qop="auth"`.
- **Repro:** `curl -sS -m 10 -k --digest -u alice:secret "ldaps://127.0.0.1:22636/ou=people,dc=example,dc=org?cn?sub?(uid=alice)"`
- **Expected (REF):** `DN: uid=alice,ou=people,dc=example,dc=org` / `\tcn: Alice Example`, exit 0.
- **Actual (.NET):** `curl: (38) LDAP local: bind via ldap_win_bind Operations Error`, exit 38. slapd logs the first SASL step answered (challenge) and then `SASL bind in progress`; the client gives up. The same over plain `ldap://` and the Linux build over ldaps both succeed.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs` `ChooseQop` and `LdapAuthenticatedBind.DigestAsync(..., acceptsQopAuth = false)` (ADR-1302). Accept `qop=auth` when the connection is already TLS; keep refusing it on plain `ldap://`.
- **Acceptance check:** the repro prints alice's entry, exit 0; a plain-`ldap://` relay that strips `auth-int`/`auth-conf` from the challenge still fails as REF does.

## Acceptance criteria

- [x] the repro prints alice's entry, exit 0; a plain-`ldap://` relay that strips `auth-int`/`auth-conf` from the challenge still fails as REF does.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Over ldaps:// DigestAsync now answers qop=auth (TLS already protects the session) and starts no confidentiality layer; plain ldap:// still refuses it as before (Operations Error). Unit test ExecuteAsync_DigestOverLdapsChallengeOffersOnlyAuth pins it. Ldap quality gate: 100/100, complexity 10. No ADR: extends ADR-1302 as the finding states.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: f5edafec 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. ldaps digest answers qop=auth
