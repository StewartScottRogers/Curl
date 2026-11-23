---
id: BL-5822
title: Fix fitness finding STD-174: Windows TLS to a server that requests a client certificate and presents an untrusted certificate exits 35; curl exits 60
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-23
---
# BL-5822 — Fix fitness finding STD-174: Windows TLS to a server that requests a client certificate and presents an untrusted certificate exits 35; curl exits 60

## Goal

STD-174 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows TLS to a server that requests a client certificate and presents an untrusted certificate exits 35; curl exits 60. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Probable (4 LDAPS option variants plus `https://`, one session)
- **Protocol/area:** Schannel TLS handshake (ldaps and https), Windows build
- **Repro:** `docker run -d --name x -p 127.0.0.1:22636:636 -e LDAP_DOMAIN=example.org -e LDAP_ADMIN_PASSWORD=admin osixia/openldap:1.5.0` (default `LDAP_TLS_VERIFY_CLIENT=demand`, self-signed certificate). `curl -sS -m 15 "ldaps://127.0.0.1:22636/dc=example,dc=org"` or `curl -sS https://127.0.0.1:22636/`.
- **Expected (REF):** `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - …`, exit 60.
- **Actual (.NET):** `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed`, exit 35.
- **Likely location:** the Networking TLS handshake: the server certificate is not verified (or its failure not reported) before the client-certificate request ends the handshake.
- **Acceptance check:** both commands exit 60 with the `SEC_E_UNTRUSTED_ROOT` text.

## Acceptance criteria

- [x] both commands exit 60 with the `SEC_E_UNTRUSTED_ROOT` text.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced with osixia/openldap:1.5.0 (TLS 1.2, ECDSA, lists acceptable CA names): the server ends the handshake before SslStream's validation callback. Fix: the Schannel/OpenSSL selection callback (called when the server requests a client certificate, with the server certificate) now judges that certificate through ServerCertificateVerification.Judge, so the failure is exit 60. A callback now always exists (it returns no certificate without --cert); Cgr169Tests pin updated. Verified: both commands exit 60 with SEC_E_UNTRUSTED_ROOT on the real server; Networking fast tests 4413 pass; Measure-CodeQuality 0 failing members.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: cc351e42 69116afe 3d836d5e.
- 2026-11-23: Doing -> Done. Windows TLS to a server demanding a client certificate with an untrusted certificate now exits 60 SEC_E_UNTRUSTED_ROOT
