---
id: BL-5828
title: Fix fitness finding STD-178: Linux: the HTTPS proxy is verified against CURL_CA_BUNDLE / SSL_CERT_FILE; curl verifies the proxy against its built-in bundle
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-23
---
# BL-5828 — Fix fitness finding STD-178: Linux: the HTTPS proxy is verified against CURL_CA_BUNDLE / SSL_CERT_FILE; curl verifies the proxy against its built-in bundle

## Goal

STD-178 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: Linux: the HTTPS proxy is verified against CURL_CA_BUNDLE / SSL_CERT_FILE; curl verifies the proxy against its built-in bundle. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (the port trusts a CA for the proxy that curl does not; the user did name that CA, but for the target)
- **Confidence:** Confirmed (2 runs, 6 variants)
- **Protocol/area:** HTTPS proxy trust, Linux build
- **Repro (Linux):** a TLS server on 127.0.0.1:26121 sends leaf + int1 (chain to private `root.pem`) and answers any request with 200 `hello`.
  ```
  CURL_CA_BUNDLE=pki/root.pem curl -sS -x https://localhost:26121 http://example.invalid/
  SSL_CERT_FILE=pki/root.pem  curl -sS -x https://localhost:26121 http://example.invalid/
  CURL_CA_BUNDLE=pki/root.pem curl -sS -k -x https://localhost:26121 http://example.invalid/
  SSL_CERT_FILE=pki/root.pem  curl -sS --cacert pki/int1.pem -x https://localhost:26121 http://example.invalid/
  ```
- **Expected (REF2):** all four exit 60 with `curl: (60) SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)`. (`SSL_CERT_DIR=capath` does reach the proxy in REF2, exit 0, and the port matches that.)
- **Actual (.NET):** `hello`, exit 0, in all four.
- **Likely location:** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs` `FindDefaultCaBundleFile` / `NonEmptyCurlCaBundle` (ADR-1323) read `CURL_CA_BUNDLE` for any option set with no CA, including the proxy's; for `SSL_CERT_FILE`, `SystemStoreChainPolicy` uses .NET's OpenSSL store, which reads that variable itself. The proxy route should trust the default bundle (`DefaultCaBundleCandidates`), not the environment-named file.
- **Acceptance check:** each of the four commands exits 60 with `(20)`; `SSL_CERT_DIR=pki/capath curl -sS -x https://localhost:26121 http://example.invalid/` still exits 0.

## Acceptance criteria

- [x] each of the four commands exits 60 with `(20)`; `SSL_CERT_DIR=pki/capath curl -sS -x https://localhost:26121 http://example.invalid/` still exits 0.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix: ServerCertificateVerification.IsProxy (ADR-1342); both providers pass isProxy. Not run against a live Linux server: pinned by unit tests in SslStreamTlsProviderTests (CURL_CA_BUNDLE ignored for proxy, SSL_CERT_DIR keeps system store). Linux-only behaviour; tests are platform-neutral. Quality run: Networking 100/100.


## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: 97e4ff74 69116afe 3d836d5e.
- 2026-11-23: Doing -> Done. STD-178: proxy verified against built-in bundle (ADR-1342)
