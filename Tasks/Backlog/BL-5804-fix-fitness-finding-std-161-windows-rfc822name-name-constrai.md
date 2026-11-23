---
id: BL-5804
title: Fix fitness finding STD-161: Windows: rfc822Name name constraints ignore the emailAddress attribute in the leaf's subject
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5804 — Fix fitness finding STD-161: Windows: rfc822Name name constraints ignore the emailAddress attribute in the leaf's subject

## Goal

STD-161 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows: rfc822Name name constraints ignore the emailAddress attribute in the leaf's subject. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common PKI used by STD-158 to STD-165 (Windows): `root.pem` is a self-signed CA. `intA.pem` is a CA signed by root (CA:TRUE, keyUsage keyCertSign+cRLSign). Leaves have SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and EKU serverAuth unless stated. Each test server is a Python `ssl` TLS server on 127.0.0.1 and ::1 that sends leaf + issuing intermediate and answers any request with `HTTP/1.1 200 OK` and body `hello <leafname>`. CRLs are served over plain http at `http://127.0.0.1:21080/...`. `<p>` is the port of the leaf's server. In the proxy variants the same TLS server acts as an HTTPS proxy and answers with the body directly. The finding, verbatim:

- **Severity:** High (accepts a chain REF refuses)
- **Confidence:** Confirmed (reviewer 2 runs each; orchestrator re-reproduced). Residue of STD-118.
- **Protocol/area:** name constraints with an intermediate `--cacert` anchor, Windows build
- **Repro:**
  1. `ncPermEmail.pem` (`permitted; email: example.com`) and `ncExclEmail.pem` (`excluded; email: other.org`), both CAs signed by root.
  2. Leaves `em_subj` (signed by ncPermEmail) and `xem_subj` (signed by ncExclEmail): subject `emailAddress=evil@other.org, CN=localhost`, SAN `DNS:localhost, IP:127.0.0.1, IP:::1`, no email SAN.
  3. `curl -sS https://localhost:<p>/ --cacert ncPermEmail.pem --ssl-no-revoke` (em_subj)
  4. `curl -sS https://localhost:<p>/ --cacert ncExclEmail.pem --ssl-no-revoke` (xem_subj)
- **Expected (REF):** step 3 `curl: (60) schannel: error 0x00004000`; step 4 `curl: (60) schannel: error 0x00008000`; exit 60.
- **Actual (.NET):** `hello em_subj` / `hello xem_subj`, exit 0. The same leaves with the address in an email SAN are refused correctly.
- **Likely location:** `Curl.Networking.UnitLibrary/CertificateNameConstraints.cs` `ListNames`: add each subject `emailAddress` (OID 1.2.840.113549.1.9.1) value as an rfc822Name, per RFC 5280 §4.2.1.10.
- **Acceptance check:** steps 3 and 4 exit 60 with `0x00004000` and `0x00008000` respectively.

## Acceptance criteria

- [ ] steps 3 and 4 exit 60 with `0x00004000` and `0x00008000` respectively.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.