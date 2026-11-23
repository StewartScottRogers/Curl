---
id: BL-5807
title: Fix fitness finding STD-163: Windows: when one of a leaf's two CRL distribution points lists it as revoked and the other is clean, the port refuses; REF accepts
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5807 — Fix fitness finding STD-163: Windows: when one of a leaf's two CRL distribution points lists it as revoked and the other is clean, the port refuses; REF accepts

## Goal

STD-163 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows: when one of a leaf's two CRL distribution points lists it as revoked and the other is clean, the port refuses; REF accepts. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common PKI used by STD-158 to STD-165 (Windows): `root.pem` is a self-signed CA. `intA.pem` is a CA signed by root (CA:TRUE, keyUsage keyCertSign+cRLSign). Leaves have SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and EKU serverAuth unless stated. Each test server is a Python `ssl` TLS server on 127.0.0.1 and ::1 that sends leaf + issuing intermediate and answers any request with `HTTP/1.1 200 OK` and body `hello <leafname>`. CRLs are served over plain http at `http://127.0.0.1:21080/...`. `<p>` is the port of the leaf's server. In the proxy variants the same TLS server acts as an HTTPS proxy and answers with the body directly. The finding, verbatim:

- **Severity:** Low (fails closed; maintainers may choose to keep this)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** revocation below an intermediate anchor, Windows build
- **Repro:** leaf `multi2_rev` (signed by intA) has two CDPs: first `http://127.0.0.1:21080/b/plainclean.crl` (valid, empty intA CRL), second `http://127.0.0.1:21080/crl/intA.crl` (lists the leaf). `curl -sS https://localhost:<p>/ --cacert intA.pem`
- **Expected (REF):** `hello multi2_rev`, exit 0 (CryptoAPI stops at the first usable CRL).
- **Actual (.NET):** `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`, exit 60.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs` `RevocationOf` combines all lists with `Any(Revokes)`.
- **Acceptance check:** the command exits 0 (or the deviation is documented as deliberate in an ADR).

## Acceptance criteria

- [ ] the command exits 0 (or the deviation is documented as deliberate in an ADR).
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
