---
id: BL-5853
title: Fix fitness finding STD-185: Linux LDAP never uses SASL PLAIN (or LOGIN, CRAM-MD5, NTLM); curl binds with PLAIN when the server offers it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5853 — Fix fitness finding STD-185: Linux LDAP never uses SASL PLAIN (or LOGIN, CRAM-MD5, NTLM); curl binds with PLAIN when the server offers it

## Goal

STD-185 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Linux LDAP never uses SASL PLAIN (or LOGIN, CRAM-MD5, NTLM); curl binds with PLAIN when the server offers it. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Medium (an important feature is broken; fails closed) · **Confidence:** Confirmed (2 runs, 3 option shapes) · **Area:** LDAP SASL mechanism choice, Linux build.
- **Setup:** a mock LDAP server on 22501 whose root DSE advertises `supportedSASLMechanisms: PLAIN`, accepts any SASL bind, and returns entry `cn=one,dc=example,dc=org` (`cn: one`).
- **Repro (Linux):** `curl -sS -m 10 -u alice:secret --anyauth ldap://127.0.0.1:22501/dc=example,dc=org`; also with `--login-options AUTH=*`, `--login-options AUTH=PLAIN`, and a server advertising `PLAIN,DIGEST-MD5` with `AUTH=PLAIN`.
- **Expected (REF2):** a SASL bind with mechanism `PLAIN` and credentials `\0alice\0secret`, then `DN: cn=one,dc=example,dc=org` / `\tcn: one`, exit 0. With LOGIN-, CRAM-MD5- or NTLM-only servers REF2 attempts those mechanisms.
- **Actual (.NET Linux):** `curl: (67) Login denied`, exit 67; no bind is sent.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs`: `BindAsync` / `AllowsDigest` consider only `LdapDigestMd5Bind.Mechanism` (ADR-1299 "limited to DIGEST-MD5", which differs from curl). Add PLAIN (and LOGIN etc.) with curl's SASL preference order and `AUTH=`/`--anyauth` allowance rules.
- **Acceptance check:** the first repro prints `DN: cn=one,dc=example,dc=org`, exit 0, and the server log shows a SASL bind with mechanism `PLAIN`.

## Acceptance criteria

- [x] the first repro prints `DN: cn=one,dc=example,dc=org`, exit 0, and the server log shows a SASL bind with mechanism `PLAIN`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decided: PLAIN is tried after DIGEST-MD5 when offered and allowed (AUTH=PLAIN/*, or the mask holding Basic when no AUTH=). LOGIN, CRAM-MD5 and NTLM are not added here. Unit tests pin the PLAIN bind (credentials are the test user cn=u,dc=x); CI platform-neutral.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 18cb9324 69116afe 3d836d5e.
