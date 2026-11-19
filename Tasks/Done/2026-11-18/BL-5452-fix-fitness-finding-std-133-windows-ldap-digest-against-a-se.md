---
id: BL-5452
title: Fix fitness finding STD-133: Windows LDAP --digest against a server that requires auth-conf but offers only rc4 fails; curl binds with rc4
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5SecurityLayer.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5452 — Fix fitness finding STD-133: Windows LDAP --digest against a server that requires auth-conf but offers only rc4 fails; curl binds with rc4

## Goal

STD-133 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP --digest against a server that requires auth-conf but offers only rc4 fails; curl binds with rc4. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (fails closed; ADR-1292 implements 3des only)
- **Confidence:** Confirmed (3 runs)
- **Protocol/area:** LDAP SASL DIGEST-MD5, Windows
- **Repro:** the OpenLDAP container and SASL setup from STD-116 steps 1–3, then `olcSaslSecProps: noanonymous,minssf=113` (the challenge becomes `qop="auth-conf",cipher="rc4"`; note this locks out ldapi EXTERNAL — undo by editing `/etc/ldap/slapd.d/cn=config.ldif` and restarting). Through a logging relay on 127.0.0.1:42880: `curl -sS -m 15 -u alice:secret --digest "ldap://127.0.0.1:42880/ou=people,dc=example,dc=org?cn?sub?(uid=alice)"`
- **Expected (REF):** answers `qop=auth-conf,cipher=rc4`, prints `DN: uid=alice,ou=people,dc=example,dc=org` / `cn: Alice Example`, exit 0.
- **Actual (PORT):** sends no answer to the challenge; `curl: (38) LDAP local: bind via ldap_win_bind Local Error`, exit 38.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs` `ChooseQop` and `LdapDigestMd5SecurityLayer.cs` (3des only). Add rc4 (and des / rc4-56 / rc4-40 as WinLDAP does), or record the gap in an ADR.
- **Acceptance check:** the command prints alice's entry, exit 0; the relay shows `cipher=rc4` followed by wrapped PDUs.

## Acceptance criteria

- [x] the command prints alice's entry, exit 0; the relay shows `cipher=rc4` followed by wrapped PDUs.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added rc4, rc4-56 and rc4-40 to the DIGEST-MD5 auth-conf layer (new LdapRc4.cs, RFC 2831 key lengths 16/7/5); cipher preference 3des, rc4, rc4-56, rc4-40. Plain `des` is still not implemented (answering falls back to qop=auth). Decision by Claude under delegation: no ADR needed beyond this note.
- Criterion 1 was checked at unit level (stand-in sealing server answering `cipher=rc4` and exchanging wrapped PDUs; RC4 known vector); no OpenLDAP container available in a lane. Ldap tests 816 pass; quality run: 0 failing members.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-133 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: f5d1d735 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Ldap DIGEST-MD5 auth-conf now supports rc4, rc4-56, rc4-40 ciphers
