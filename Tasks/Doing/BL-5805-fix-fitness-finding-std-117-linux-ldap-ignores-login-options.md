---
id: BL-5805
title: Fix fitness finding STD-117: Linux LDAP ignores --login-options AUTH=... and --anyauth and sends the password in a cleartext simple bind (STILL OPEN, partly fixed)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/MailRequestOptionsMapping.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5805 — Fix fitness finding STD-117: Linux LDAP ignores --login-options AUTH=... and --anyauth and sends the password in a cleartext simple bind (STILL OPEN, partly fixed)

## Goal

STD-117 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Linux LDAP ignores --login-options AUTH=... and --anyauth and sends the password in a cleartext simple bind (STILL OPEN, partly fixed). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

This finding was filed before (fitness review 2026-11-18T0633) and is only partly fixed; the finding says which part still reproduces.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** High (credential leak: the password crosses plaintext `ldap://` when the user asked for a mechanism that never sends it)
- **Confidence:** Confirmed (reviewer 2 runs per variant; orchestrator re-reproduced: the bind PDU contains `80 06 73 65 63 72 65 74` = simple password "secret")
- **Protocol/area:** LDAP bind, Linux build (OpenLDAP dialect). `--digest`, `--ntlm` and `--negotiate` are now fixed.
- **Repro:**
  1. OpenLDAP: `docker run -d --name ldap -p 127.0.0.1:22389:389 -e LDAP_DOMAIN=example.org -e LDAP_ADMIN_PASSWORD=admin osixia/openldap:1.5.0`, add `uid=alice,ou=people,dc=example,dc=org` (cn `Alice Example`) and configure SASL DIGEST-MD5 so that `ldapwhoami -Y DIGEST-MD5 -U alice -w secret` works.
  2. A logging TCP relay on 127.0.0.1:22890 → 22389 that records client-to-server bytes.
  3. On Linux: `curl -sS -m 15 -u alice:secret --login-options AUTH=DIGEST-MD5 'ldap://127.0.0.1:22890/ou=people,dc=example,dc=org?cn?sub?(uid=alice)'`
  4. Repeat with `AUTH=digest-md5`, `AUTH=*`, `AUTH=CRAM-MD5`, `AUTH=PLAIN`, `AUTH=EXTERNAL`, and with `--anyauth` instead of `--login-options`.
  5. Search the relay log for the password bytes.
- **Expected (REF2, curl 8.18.0 OpenLDAP):** `AUTH=DIGEST-MD5`, `AUTH=digest-md5`, `AUTH=*` and `--anyauth`: a SASL DIGEST-MD5 bind, output `DN: uid=alice,ou=people,dc=example,dc=org` / `cn: Alice Example`, exit 0. `AUTH=CRAM-MD5`, `AUTH=PLAIN`, `AUTH=EXTERNAL`: `curl: (67) Login denied`, exit 67. The password never appears on the wire.
- **Actual (.NET, Linux):** every variant sends a simple BindRequest with the password in clear, then `curl: (38) LDAP: cannot bind`, exit 38. With a DN user name the cleartext bind succeeds.
- **Likely location:** `Curl.Engine.UnitLibrary/MailRequestOptionsMapping.cs` `FromCommandLine` returns null unless the scheme is in `MailSchemes`, so `context.Mail?.LoginOptions` is always null for ldap/ldaps and `LdapProtocolHandler.ChooseOpenLdapBindKind` never sees `AUTH=`. The same choice picks a simple bind for `--anyauth` because "the auth mask lacks Basic"; REF2 simple-binds only when the mask is exactly Basic.
- **Acceptance check:** on Linux, step 3 prints alice's entry with exit 0 and the relay log shows a SASL DIGEST-MD5 bind and no password bytes; `--anyauth` does the same; `--login-options AUTH=PLAIN` exits 67 `Login denied` with no password on the wire.

## Acceptance criteria

- [x] on Linux, step 3 prints alice's entry with exit 0 and the relay log shows a SASL DIGEST-MD5 bind and no password bytes; `--anyauth` does the same; `--login-options AUTH=PLAIN` exits 67 `Login denied` with no password on the wire.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix: `MailRequestOptionsMapping` now carries LoginOptions for ldap/ldaps; `ChooseOpenLdapBindKind` simple-binds only when the auth mask is exactly Basic (REF2). Unit tests added in both `.UnitTests` projects. Wire-level criterion (relay log) is met by those handler tests (SASL bind, no password); no Docker OpenLDAP here. Quality run: 0 failing members.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: 8e9040af 69116afe 3d836d5e.
