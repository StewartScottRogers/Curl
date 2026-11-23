---
id: BL-5827
title: Fix fitness finding STD-131: Linux --capath / --proxy-capath accept a chain completed from ~/.dotnet/corefx/cryptography/x509stores/ca (STILL OPEN, partly fixed)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5827 — Fix fitness finding STD-131: Linux --capath / --proxy-capath accept a chain completed from ~/.dotnet/corefx/cryptography/x509stores/ca (STILL OPEN, partly fixed)

## Goal

STD-131 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Linux --capath / --proxy-capath accept a chain completed from ~/.dotnet/corefx/cryptography/x509stores/ca (STILL OPEN, partly fixed). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

This finding was filed before (fitness review 2026-11-18T0633) and is only partly fixed; the finding says which part still reproduces.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (trust anchored in a hidden per-user store the user never configured)
- **Confidence:** Confirmed (3 runs `--capath`, 2 runs `--proxy-capath`)
- **Protocol/area:** HTTPS and HTTPS-proxy chain building, Linux build. `--cacert`, `SSL_CERT_FILE`, `CURL_CA_BUNDLE`, `--proxy-cacert` and `SSL_CERT_DIR` are fixed.
- **Repro (Linux):**
  1. PKI: `root.pem` → `int1.pem` (CA) → leaf `leaf1noaia` (SAN `localhost`, no AIA). A TLS server on 127.0.0.1:26103 sends the leaf alone (no int1) and answers `hello`.
  2. `pki/capath/` holds `root.pem` only (after `openssl rehash`).
  3. Plant int1 in the .NET store:
     ```
     export HOME=/tmp/a6home; CA=$HOME/.dotnet/corefx/cryptography/x509stores/ca; mkdir -p $CA
     SHA=$(openssl x509 -in pki/int1.pem -noout -fingerprint -sha1 | cut -d= -f2 | tr -d :)
     openssl pkcs12 -export -nokeys -in pki/int1.pem -out $CA/$SHA.pfx -passout pass:
     ```
  4. `curl -sS -w '%{ssl_verify_result}\n' --capath pki/capath https://localhost:26103/`
  5. `curl -sS --proxy-capath pki/capath -x https://localhost:26103 http://example.invalid/`
- **Expected (REF2):** `curl: (60) SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)`, exit 60, `%{ssl_verify_result}` 20.
- **Actual (.NET):** `hello` and `0`, exit 0, for both. With the store emptied, `--capath` exits 60.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs` `RelyingOnAnIntermediateTheServerDidNotSend` inspects only the chain SslStream built; on the `--capath` route that chain does not end at an anchor, so the check sees nothing. The chain that actually accepts is rebuilt by `ServerCertificateVerification.CaCertificates.cs` (`RebuildAgainstAnchors`, `ChainLeadsToAnyOf`, `ReasonChainFromAnchors`); apply the "sent by the peer" test to it.
- **Acceptance check:** with the PFX planted, commands 4 and 5 exit 60 with `(20)` and `%{ssl_verify_result}` 20; `--capath pki/capath` against a server that sends leaf + int1 still exits 0.

## Acceptance criteria

- [ ] with the PFX planted, commands 4 and 5 exit 60 with `(20)` and `%{ssl_verify_result}` 20; `--capath pki/capath` against a server that sends leaf + int1 still exits 0.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: 400696f7 69116afe 3d836d5e.
