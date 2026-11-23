---
id: BL-5806
title: Fix fitness finding STD-162: Windows: a CRL entry with a certificateIssuer extension or an unknown critical entry extension makes the whole CRL unusable; REF ignores the entry
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/RevocationListScope.cs, Curl.Networking.UnitLibrary/CertificateRevocationList.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5806 — Fix fitness finding STD-162: Windows: a CRL entry with a certificateIssuer extension or an unknown critical entry extension makes the whole CRL unusable; REF ignores the entry

## Goal

STD-162 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows: a CRL entry with a certificateIssuer extension or an unknown critical entry extension makes the whole CRL unusable; REF ignores the entry. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common PKI used by STD-158 to STD-165 (Windows): `root.pem` is a self-signed CA. `intA.pem` is a CA signed by root (CA:TRUE, keyUsage keyCertSign+cRLSign). Leaves have SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and EKU serverAuth unless stated. Each test server is a Python `ssl` TLS server on 127.0.0.1 and ::1 that sends leaf + issuing intermediate and answers any request with `HTTP/1.1 200 OK` and body `hello <leafname>`. CRLs are served over plain http at `http://127.0.0.1:21080/...`. `<p>` is the port of the leaf's server. In the proxy variants the same TLS server acts as an HTTPS proxy and answers with the body directly. The finding, verbatim:

- **Severity:** Low (fails closed)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** revocation below an intermediate anchor, Windows build
- **Repro:** leaves `v_entrycrit` and `v_certissuer` (signed by intA) have a CDP to an intA CRL with one entry for serial 123456789 (not theirs). That entry carries a critical unknown extension `1.3.6.1.4.1.99999.2` (first leaf) or a critical certificateIssuer extension (second leaf). Run `curl -sS https://localhost:<p>/ --cacert intA.pem` for each.
- **Expected (REF):** `hello v_entrycrit` / `hello v_certissuer`, exit 0.
- **Actual (.NET):** `curl: (60) schannel: the revocation status is unknown`, exit 60.
- **Likely location:** `Curl.Networking.UnitLibrary/RevocationListScope.cs` `EntryExtensionsAreUnusable`, called from `CertificateRevocationList.ReadRevokedSerialNumbers`.
- **Acceptance check:** both commands exit 0; a CRL with an unknown critical list-level (not entry) extension still exits 60 "unknown".

## Acceptance criteria

- [ ] both commands exit 0; a CRL with an unknown critical list-level (not entry) extension still exits 60 "unknown".
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
