---
id: BL-5461
title: Fix fitness finding STD-142: Windows ldaps:// with -u and an untrusted server certificate exits 38 "Server Down"; curl exits 60
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5461 — Fix fitness finding STD-142: Windows ldaps:// with -u and an untrusted server certificate exits 38 "Server Down"; curl exits 60

## Goal

STD-142 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Windows ldaps:// with -u and an untrusted server certificate exits 38 "Server Down"; curl exits 60. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (both fail closed; wrong exit code and message)
- **Confidence:** Confirmed (3 option variants, 2 runs)
- **Protocol/area:** LDAPS, Windows
- **Repro:** `docker run -d -p 127.0.0.1:42636:636 -e LDAP_DOMAIN=example.org -e LDAP_ADMIN_PASSWORD=admin osixia/openldap:1.5.0` (serves its own self-signed certificate, not in the Windows store). `curl -sS -m 15 -u cn=admin,dc=example,dc=org:admin "ldaps://127.0.0.1:42636/ou=people,dc=example,dc=org?cn?sub?(uid=alice)"`; also with `-u alice:secret --digest`, and with `--ssl-no-revoke`.
- **Expected (REF):** `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.`, exit 60. Without `-u` the PORT already matches.
- **Actual (PORT):** `curl: (38) LDAP local: bind via ldap_win_bind Server Down`, exit 38. This contradicts ADR-0994's stated consequence ("failures of curl's own first connection … still exit 60").
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs` around `winLdapChecksCertificate` (~line 170): with credentials, curl's own first handshake result is not reported.
- **Acceptance check:** the command exits 60 with the `SEC_E_UNTRUSTED_ROOT` message.

## Acceptance criteria

- [ ] the command exits 60 with the `SEC_E_UNTRUSTED_ROOT` message.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-142 (Low).