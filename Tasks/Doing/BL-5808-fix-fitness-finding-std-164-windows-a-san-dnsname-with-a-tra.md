---
id: BL-5808
title: Fix fitness finding STD-164: Windows: a SAN dNSName with a trailing dot (localhost.) does not match host localhost; REF accepts it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.NameChecks.cs, Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5808 — Fix fitness finding STD-164: Windows: a SAN dNSName with a trailing dot (localhost.) does not match host localhost; REF accepts it

## Goal

STD-164 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows: a SAN dNSName with a trailing dot (localhost.) does not match host localhost; REF accepts it. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common PKI used by STD-158 to STD-165 (Windows): `root.pem` is a self-signed CA. `intA.pem` is a CA signed by root (CA:TRUE, keyUsage keyCertSign+cRLSign). Leaves have SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and EKU serverAuth unless stated. Each test server is a Python `ssl` TLS server on 127.0.0.1 and ::1 that sends leaf + issuing intermediate and answers any request with `HTTP/1.1 200 OK` and body `hello <leafname>`. CRLs are served over plain http at `http://127.0.0.1:21080/...`. `<p>` is the port of the leaf's server. In the proxy variants the same TLS server acts as an HTTPS proxy and answers with the body directly. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 leaves)
- **Protocol/area:** HTTPS host-name check, Windows build
- **Repro:** leaf `plain_trailingdot` (signed by intA) has SAN `DNS:localhost.` only. `curl -sS https://localhost:<p>/ --cacert intA.pem --ssl-no-revoke`; repeat with URL host `localhost.`.
- **Expected (REF):** `hello plain_trailingdot`, exit 0, for both hosts.
- **Actual (.NET):** `curl: (60) schannel: CertGetNameString() failed to match connection hostname (localhost) against server certificate names`, exit 60, for both. A leaf with `CN=localhost.` and no SAN matches REF.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.NameChecks.cs` / `SchannelCommonNameCheck.cs`: strip one trailing dot from both the SAN name and the host before comparing.
- **Acceptance check:** both commands exit 0.

## Acceptance criteria

- [ ] both commands exit 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: 59e6c62b 69116afe 3d836d5e.
