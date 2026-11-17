---
id: BL-5310
title: Reject a server or proxy certificate whose EKU forbids server authentication when trust comes from --capath or SSL_CERT_DIR
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5310 — Reject a server or proxy certificate whose EKU forbids server authentication when trust comes from --capath or SSL_CERT_DIR

## Goal

On Linux, a server or HTTPS-proxy certificate whose extended key usage does not allow server authentication (clientAuth-only, codeSigning-only) is rejected with `curl: (60) SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)` when trust comes from `--capath`, `--proxy-capath` or `SSL_CERT_DIR`, as it already is with `--cacert`.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-072 (High, Confirmed: 2+ runs on 6 routes, re-reproduced by the orchestrator). Area: HTTPS server and HTTPS proxy certificate verification, trust from a CA directory, Linux build. Security weakening: a clientAuth-only or codeSigning-only certificate issued by a trusted CA can impersonate a TLS server.

**Repro (Linux).**
1. Make a test CA: `openssl req -x509 -newkey rsa:2048 -nodes -keyout ca.key -out ca.pem -days 2 -subj /CN=TestCA -addext basicConstraints=critical,CA:TRUE -addext keyUsage=keyCertSign,cRLSign`
2. Make a CA directory: `mkdir capath && cp ca.pem capath/ && openssl rehash capath`
3. Issue a leaf with `subjectAltName=DNS:localhost` and `extendedKeyUsage=clientAuth` (and a second with `extendedKeyUsage=codeSigning`), signed by the CA.
4. Serve the leaf with a TLS server on 127.0.0.1:35106 (the review used Python `ssl.SSLContext(PROTOCOL_TLS_SERVER)`; here use PowerShell or a C# file-based app, never Python), answering `HTTP/1.1 200 OK` with body `hello`.
5. Run:
   - `curl -sS --capath capath https://localhost:35106/`
   - `SSL_CERT_DIR=$PWD/capath curl -sS https://localhost:35106/`
   - `curl -sS --capath capath --curves P-256 https://localhost:35106/`
   - serve the same leaf as an HTTPS proxy on 35303, then `curl -sS -x https://localhost:35303 --proxy-capath capath http://127.0.0.1:35080/`

**Expected (reference curl 8.18, OpenSSL build).** `curl: (60) SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)`, exit 60, for every command and both leaves. The port itself gives exactly this with `--cacert ca.pem`, or `--cacert` plus `--capath`.

**Actual (port).** Prints `hello`, exit 0, for every command above.

**Where to start.** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`. `ChainLeadsToAnyOf` (about line 783) builds `anchoredChain` with `CustomRootTrust` but never adds `ApplicationPolicy.Add(ServerAuthentication)`; `WithoutChainErrorsCurlTolerates` (about line 499) then clears all `RemoteCertificateChainErrors`. `ReadChainAnchors` (about line 202) / `SystemStoreChainPolicy` return a null policy on the OpenSSL build when only a directory is given. Add the serverAuth application policy to `anchoredChain` and keep the original chain's NotValidForUsage status. Check the Linux system-store route too (not testable on a lane without root; say so in the Log).

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests/ServerCertificateVerificationTests.CaDirectoryPurpose.cs` (platform-neutral, certificates generated in the test with `CertificateRequest`, no network) prove that a leaf signed by a CA given only through the CA-directory / `SSL_CERT_DIR` route and carrying only the `clientAuth` EKU is rejected as not valid for usage (the exit 60, "unsuitable certificate purpose" path), and the same for `codeSigning` only.
- [ ] A test proves the same rejection for the HTTPS-proxy policy (`--proxy-capath`).
- [ ] A test proves a leaf with the `serverAuth` EKU (and one with no EKU) signed by the same CA-directory anchor is still accepted.
- [ ] A test proves the existing `--cacert` rejection of the clientAuth-only leaf is unchanged.
- [ ] The Log records the review's hand check on Linux, or says it could not be run: all four commands exit 60 with `unsuitable certificate purpose (26)`, and `curl -sS --capath capath https://localhost:<serverAuth leaf port>/` still exits 0.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Match the OpenSSL build's text on Linux and macOS (measure with `Record-CurlExchange.ps1` where a reference curl is available). Mock servers must be PowerShell or a C# file-based app, never Python (CLAUDE.md).
- Tests must pass on Windows, Linux and macOS; do not depend on `openssl rehash` in a unit test.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 356a4b62 c116be91 6027781a ef4e6168 5c150c3e a0b49a29.
