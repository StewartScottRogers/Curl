---
id: BL-5435
title: Fix fitness finding STD-117: Linux LDAP ignores --digest, --ntlm, --negotiate and --login-options AUTH=… and sends the password in a cleartext simple bind
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5435 — Fix fitness finding STD-117: Linux LDAP ignores --digest, --ntlm, --negotiate and --login-options AUTH=… and sends the password in a cleartext simple bind

## Goal

STD-117 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Linux LDAP ignores --digest, --ntlm, --negotiate and --login-options AUTH=… and sends the password in a cleartext simple bind. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** High (credential leak: the user asked for a mechanism that never sends the password, and the password crosses plaintext `ldap://`)
- **Confidence:** Confirmed (5 option variants, 2 runs each; re-reproduced by the orchestrator for `--digest`, `AUTH=DIGEST-MD5` and `--ntlm`)
- **Protocol/area:** LDAP bind, Linux build (OpenLDAP dialect)
- **Repro:**
  1. The OpenLDAP container and SASL setup from STD-116 steps 1–3 (alice with cleartext password `secret`).
  2. In WSL/Linux, a TCP relay on 127.0.0.1:42882 → 127.0.0.1:42389 that logs client-to-server bytes.
  3. `curl -sS -m 15 -u alice:secret --digest 'ldap://127.0.0.1:42882/ou=people,dc=example,dc=org?cn?sub?(uid=alice)'`; also with `--login-options AUTH=DIGEST-MD5`, with `--ntlm`, with `--negotiate`, and with `--login-options AUTH=CRAM-MD5` instead of `--digest`.
  4. Count `secret` in the client-to-server bytes.
- **Expected (REF2, curl 8.18.0 OpenLDAP):** `--digest` and `AUTH=DIGEST-MD5`: a rootDSE read, then a SASL DIGEST-MD5 bind; prints `DN: uid=alice,…` / `cn: Alice Example`, exit 0; `secret` appears 0 times. `--ntlm`, `--negotiate`, `AUTH=CRAM-MD5` (not offered by this server): `curl: (67) Login denied`, exit 67; `secret` appears 0 times.
- **Actual (PORT, Linux):** every variant sends a 25-byte simple BindRequest with name `alice` and password `secret` in clear, then exits 38 `curl: (38) LDAP: cannot bind`. With a DN user (`-u uid=alice,ou=people,dc=example,dc=org:secret --digest`) the cleartext bind succeeds and the search runs.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs` `ChooseBindKind` returns `LdapBindKind.Simple` whenever the dialect is not WinLdap. The OpenLDAP dialect needs curl's SASL selection (the auth mask and `AUTH=` matched against supportedSASLMechanisms), or it must refuse.
- **Acceptance check:** on Linux, the `--digest` command prints alice's entry, exit 0, and the relay log shows a SASL bind (`0xa3`, `DIGEST-MD5`) and no `secret`. The `--ntlm` command exits 67 `Login denied` with no `secret` on the wire.

## Acceptance criteria

- [ ] on Linux, the `--digest` command prints alice's entry, exit 0, and the relay log shows a SASL bind (`0xa3`, `DIGEST-MD5`) and no `secret`. The `--ntlm` command exits 67 `Login denied` with no `secret` on the wire.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-117 (High).