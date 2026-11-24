---
id: BL-5852
title: Fix fitness finding STD-184: Linux LDAP: a malformed --login-options value makes the port send the password in a cleartext simple bind; curl refuses with exit 49 before connecting
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5852 — Fix fitness finding STD-184: Linux LDAP: a malformed --login-options value makes the port send the password in a cleartext simple bind; curl refuses with exit 49 before connecting

## Goal

STD-184 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Linux LDAP: a malformed --login-options value makes the port send the password in a cleartext simple bind; curl refuses with exit 49 before connecting. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Medium (security-relevant: the user asked for SASL and the port falls back to sending the password in clear; listed as a blocker) · **Confidence:** Confirmed (2 runs, several shapes) · **Area:** LDAP bind option parsing, Linux build. Next to this round's STD-117 fix.
- **Setup:** OpenLDAP (osixia/openldap:1.5.0) on 127.0.0.1:22389 with user `uid=alice,ou=people,dc=example,dc=org` password `secret`; a logging TCP relay on 22890 forwarding to 22389 that counts occurrences of `secret` in client-to-server bytes.
- **Repro (Linux):**
  - `curl -sS -m 15 -u alice:secret --login-options AUTH:DIGEST-MD5 'ldap://127.0.0.1:22890/ou=people,dc=example,dc=org?cn?sub?(uid=alice)'`
  - Same with `--login-options` values `DIGEST-MD5`, `AUTHDIGEST-MD5`, `SASL=DIGEST-MD5`, `FOO=bar`.
  - `curl -sS -m 15 -u uid=alice,ou=people,dc=example,dc=org:secret --login-options AUTH:DIGEST-MD5 'ldap://127.0.0.1:22890/ou=people,dc=example,dc=org?cn?sub?(uid=alice)'`
- **Expected (REF2):** `curl: (49) Malformed option provided in a setopt`, exit 49, no connection made. REF2 also returns 49 for `AUTH=BOGUS`, `AUTH=`, `AUTH=+LOGIN`, `"AUTH=DIGEST-MD5 AUTH=PLAIN"`, `" AUTH=DIGEST-MD5"`, `;AUTH=DIGEST-MD5`, `AUTH=DIGEST-MD5;FOO=1`, `AUTH=DIGEST-MD5;AUTH=BOGUS`.
- **Actual (.NET Linux):** the first group sends a simple BindRequest with `secret` in clear (relay count 1) then `curl: (38) LDAP: cannot bind`, exit 38. With the full DN as user the cleartext bind succeeds and alice's entry is printed, exit 0. The second group gives exit 67 or a successful SASL bind (no password sent) instead of 49.
- **Likely location:** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs`: `HasAuthOption` and `SplitOptions` ignore unknown, empty or non-mechanism items. Validate like curl's `Curl_sasl_parse_url_auth_option`: only `AUTH=<known mechanism or *>` items, no empty items; otherwise fail with CURLE_SETOPT_OPTION_SYNTAX (49) before connecting.
- **Acceptance check:** the first repro prints `curl: (49) Malformed option provided in a setopt`, exits 49, and the relay sees no connection.

## Acceptance criteria

- [ ] the first repro prints `curl: (49) Malformed option provided in a setopt`, exits 49, and the relay sees no connection.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: a7f1b18a 69116afe 3d836d5e.
