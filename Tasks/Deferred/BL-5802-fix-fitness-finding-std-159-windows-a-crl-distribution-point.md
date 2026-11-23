---
id: BL-5802
title: Fix fitness finding STD-159: Windows: a CRL distribution point limited to some reasons is treated as a complete CRL
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5802 — Fix fitness finding STD-159: Windows: a CRL distribution point limited to some reasons is treated as a complete CRL

## Goal

STD-159 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows: a CRL distribution point limited to some reasons is treated as a complete CRL. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common PKI used by STD-158 to STD-165 (Windows): `root.pem` is a self-signed CA. `intA.pem` is a CA signed by root (CA:TRUE, keyUsage keyCertSign+cRLSign). Leaves have SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and EKU serverAuth unless stated. Each test server is a Python `ssl` TLS server on 127.0.0.1 and ::1 that sends leaf + issuing intermediate and answers any request with `HTTP/1.1 200 OK` and body `hello <leafname>`. CRLs are served over plain http at `http://127.0.0.1:21080/...`. `<p>` is the port of the leaf's server. In the proxy variants the same TLS server acts as an HTTPS proxy and answers with the body directly. The finding, verbatim:

- **Severity:** High (security weakening: a partial CRL is accepted as proof of non-revocation; real impact is limited to CAs that partition CRLs by reason)
- **Confidence:** Confirmed (reviewer 3 runs plus proxy; orchestrator re-reproduced)
- **Protocol/area:** HTTPS and HTTPS-proxy revocation below an intermediate anchor, Windows build
- **Repro:**
  1. Leaf `reason_cdp` (signed by intA) has a CRLDistributionPoints entry with fullName `http://127.0.0.1:21080/b/plainclean2.crl` and `reasons = {keyCompromise}`. The URL serves a valid, empty CRL signed by intA.
  2. `curl -sS https://localhost:<p>/ --cacert intA.pem`
  3. `curl -sS -x https://localhost:<p> --proxy-cacert intA.pem http://127.0.0.1:21999/x`
  4. Variants: the DP lists all 8 reasons (`reason_allcdp`); the leaf is listed as revoked in that DP's CRL (`reason_rev`).
- **Expected (REF):** `curl: (60) schannel: the revocation status is unknown`, exit 60, for steps 2, 3 and both variants.
- **Actual (.NET):** `hello reason_cdp`, exit 0, for steps 2 and 3; `reason_allcdp` exit 0; `reason_rev` exit 60 with `has been revoked` (same code, different text).
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs` `AddUrisOfPoint` ignores the DistributionPoint `reasons [1]` field. A point carrying `reasons` should yield "revocation status unknown", as CryptoAPI does.
- **Acceptance check:** step 2 exits 60 with `schannel: the revocation status is unknown`; a leaf with a plain DP (no `reasons`) and an empty CRL still exits 0.

## Acceptance criteria

- [ ] step 2 exits 60 with `schannel: the revocation status is unknown`; a leaf with a plain DP (no `reasons`) and an empty CRL still exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
