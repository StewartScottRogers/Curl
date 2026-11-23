---
id: BL-5801
title: Fix fitness finding STD-158: Windows: delta CRLs (FreshestCRL) are ignored below an intermediate-only --cacert / --proxy-cacert anchor, so a certificate revoked in the delta CRL is accepted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/RevocationListScope.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-23
---
# BL-5801 — Fix fitness finding STD-158: Windows: delta CRLs (FreshestCRL) are ignored below an intermediate-only --cacert / --proxy-cacert anchor, so a certificate revoked in the delta CRL is accepted

## Goal

STD-158 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Windows: delta CRLs (FreshestCRL) are ignored below an intermediate-only --cacert / --proxy-cacert anchor, so a certificate revoked in the delta CRL is accepted. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common PKI used by STD-158 to STD-165 (Windows): `root.pem` is a self-signed CA. `intA.pem` is a CA signed by root (CA:TRUE, keyUsage keyCertSign+cRLSign). Leaves have SAN `DNS:localhost, IP:127.0.0.1, IP:::1` and EKU serverAuth unless stated. Each test server is a Python `ssl` TLS server on 127.0.0.1 and ::1 that sends leaf + issuing intermediate and answers any request with `HTTP/1.1 200 OK` and body `hello <leafname>`. CRLs are served over plain http at `http://127.0.0.1:21080/...`. `<p>` is the port of the leaf's server. In the proxy variants the same TLS server acts as an HTTPS proxy and answers with the body directly. The finding, verbatim:

- **Severity:** High (security weakening: a revoked server or proxy certificate is accepted in the default configuration)
- **Confidence:** Confirmed (reviewer 4 runs plus the proxy case; orchestrator re-reproduced direct and proxy)
- **Protocol/area:** HTTPS and HTTPS-proxy revocation, Windows build (port's own revocation check used when `--cacert` holds only an intermediate)
- **Repro:**
  1. Leaf `v_freshest` (signed by intA) has CRL distribution point `http://127.0.0.1:21080/b/freshest.crl`. That URL serves a valid base CRL signed by intA with no revoked entries, a CRLNumber, and a non-critical FreshestCRL extension (OID 2.5.29.46) pointing to `http://127.0.0.1:21080/b/delta_rev.crl`.
  2. `delta_rev.crl` is signed by intA, has a critical `deltaCRLIndicator` = 1, and lists `v_freshest`'s serial as revoked.
  3. Second shape: leaf `leaf_freshest` has a CDP to a clean base CRL, and the leaf itself carries a FreshestCRL extension pointing to a delta CRL that lists it.
  4. `curl -sS https://localhost:<p>/ --cacert intA.pem`
  5. `curl -sS -x https://localhost:<p> --proxy-cacert intA.pem http://127.0.0.1:21999/x`
  6. Variants: the FreshestCRL URL answers HTTP 404, or is an `ldap://` URL.
- **Expected (REF):** steps 4 and 5: `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`, exit 60. Variant 6: `curl: (60) schannel: the revocation status is unknown`, exit 60. Controls: a valid empty delta gives exit 0; `--ssl-revoke-best-effort` with a 404 delta gives exit 0.
- **Actual (.NET):** `hello v_freshest` (or `hello leaf_freshest`), exit 0, for steps 4, 5 and both variants. The delta URL is never fetched. With `--cacert root.pem` the port matches REF.
- **Likely location:** `Curl.Networking.UnitLibrary/RevocationListScope.cs` `ReadListExtension` (FreshestCRL is on the ignore list); `ServerCertificateVerification.AnchorRevocation.cs` `RevocationOf` (the certificate's FreshestCRL extension is never read). Fetch the delta named by the base CRL or the certificate, validate it like the base (issuer, signature, time, scope, `deltaCRLIndicator` ≤ base CRLNumber), apply its entries, and treat an unfetchable or unusable delta as "revocation status unknown".
- **Acceptance check:** step 4 for `v_freshest` exits 60 with `has been revoked`; the 404 variant exits 60 with `the revocation status is unknown`; a leaf whose delta is valid and empty still exits 0.

## Acceptance criteria

- [x] step 4 for `v_freshest` exits 60 with `has been revoked`; the 404 variant exits 60 with `the revocation status is unknown`; a leaf whose delta is valid and empty still exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delta CRLs are now read below an intermediate-only anchor: the base list's freshestCRL and the leaf's freshestCRL are fetched, checked like the base (issuer, signature, nextUpdate, deltaCRLIndicator <= base cRLNumber), and a missing, unusable or non-http delta leaves the status unknown (exit 60). The ldap:// variant is therefore unknown, as in curl. Unit tests: IntermediateOnlyProxyCacertTests. Quality run for Curl.Networking.UnitLibrary: 100/100, no failing member. No ADR needed (a fix to match curl).

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: 6ccddf78 69116afe 3d836d5e.
- 2026-11-23: Doing -> Done. Delta CRLs are fetched and applied below an intermediate-only anchor; quality gate clean
