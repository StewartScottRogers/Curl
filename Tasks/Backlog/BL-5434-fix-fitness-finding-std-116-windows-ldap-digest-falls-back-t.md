---
id: BL-5434
title: Fix fitness finding STD-116: Windows LDAP --digest falls back to qop=auth (no security layer) when the challenge lacks auth-conf with 3des; curl refuses to bind, so a man-in-the-middle can strip the protection
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5434 — Fix fitness finding STD-116: Windows LDAP --digest falls back to qop=auth (no security layer) when the challenge lacks auth-conf with 3des; curl refuses to bind, so a man-in-the-middle can strip the protection

## Goal

STD-116 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows LDAP --digest falls back to qop=auth (no security layer) when the challenge lacks auth-conf with 3des; curl refuses to bind, so a man-in-the-middle can strip the protection. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** High (security downgrade: the session runs unencrypted and unprotected where REF fails closed; the downgrade is silent)
- **Confidence:** Confirmed (server offering only `auth`: 3 runs; MITM rewrite: 2 runs by the reviewer and 2 by the orchestrator)
- **Protocol/area:** LDAP SASL DIGEST-MD5 bind, Windows
- **Repro:**
  1. `docker run -d --name t-ldap -p 127.0.0.1:42389:389 -e LDAP_DOMAIN=example.org -e LDAP_ADMIN_PASSWORD=admin -e LDAP_TLS=false osixia/openldap:1.5.0`
  2. Add `ou=people,dc=example,dc=org` and `uid=alice,ou=people,dc=example,dc=org` (inetOrgPerson, `cn: Alice Example`, `sn: Example`, `userPassword: secret` in cleartext, `description: hello world`) with `ldapadd -x -D cn=admin,dc=example,dc=org -w admin`.
  3. Inside the container, `ldapmodify -Y EXTERNAL -H ldapi:///`: on `cn=config` set `olcAuthzRegexp: {0}uid=([^,]*),(cn=[^,]*,)?cn=digest-md5,cn=auth uid=$1,ou=people,dc=example,dc=org` and `olcSaslSecProps: noanonymous,noplain,maxssf=256`; on `olcDatabase={1}mdb,cn=config` set olcAccess `{0}to * by dn.exact=gidNumber=0+uidNumber=0,cn=peercred,cn=external,cn=auth manage by * break`, `{1}to attrs=userPassword by self write by dn="cn=admin,dc=example,dc=org" write by anonymous auth by * none`, `{2}to * by * read`. Check: `ldapwhoami -H ldap://localhost -Y DIGEST-MD5 -U alice -w secret` prints alice's DN.
  4. A MITM TCP relay on 127.0.0.1:42881 → 42389 that, in server-to-client data, replaces the regex `qop="auth,auth-int,auth-conf",cipher="[^"]*"` with a same-length string `qop="auth",x="yyy…y"` (no BER length changes) and logs both directions.
  5. `curl -sS -m 15 -u alice:secret --digest "ldap://127.0.0.1:42881/ou=people,dc=example,dc=org?cn,description?sub?(uid=alice)"`
  6. Without a MITM: set `olcSaslSecProps: noanonymous,maxssf=0` (the server then offers only `qop="auth"`), restart, and run step 5 against a plain logging relay.
- **Expected (REF):** no digest-response is sent; `curl: (38) LDAP local: bind via ldap_win_bind Operations Error`, exit 38; no search data crosses the wire.
- **Actual (PORT):** it answers with `qop=auth`, the bind succeeds, and the SearchRequest and the result (`Alice Example`, `hello world`) travel as plain BER (first byte `0x30`). It prints the entry, exit 0, with no warning.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5Bind.cs` `ChooseQop` ("auth-conf when offered with 3des, else auth"). ADR-1292 chose this fallback, but it contradicts the WinLDAP behaviour the ADR's own context cites. In the WinLDAP dialect refuse the bind when auth-conf with a supported cipher is not offered.
- **Acceptance check:** steps 5 and 6 exit 38 with `curl: (38) LDAP local: bind via ldap_win_bind Operations Error`; the relay log shows no `qop=auth` response and no plaintext SearchRequest. The unmodified server still binds with `qop=auth-conf,cipher=3des`.

## Acceptance criteria

- [ ] steps 5 and 6 exit 38 with `curl: (38) LDAP local: bind via ldap_win_bind Operations Error`; the relay log shows no `qop=auth` response and no plaintext SearchRequest. The unmodified server still binds with `qop=auth-conf,cipher=3des`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-116 (High).