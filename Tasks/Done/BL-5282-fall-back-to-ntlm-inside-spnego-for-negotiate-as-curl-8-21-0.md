---
id: BL-5282
title: Fall back to NTLM inside SPNEGO for --negotiate, as curl 8.21.0 as tagged does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SspiNegotiateSecurityContext.cs, Curl.Authentication.UnitTests/SpnegoNtlmFallbackTests.cs]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5282 — Fall back to NTLM inside SPNEGO for --negotiate, as curl 8.21.0 as tagged does

## Goal

With `--negotiate` against a host with no Kerberos realm, the port's SPNEGO offers NTLMSSP and logs in, as upstream curl 8.21.0 as tagged does; or the port records that it follows upstream's later `!ntlm` change instead.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 4 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http.md`, finding 4.
- **Surl tests (2):** `CurlPortBesideUpstreamCurlOverHttpTests.NegotiateOverHttp_Account_ExitsZeroWithTheFilesBytes` and `NegotiateOverHttp_WrongPassword_IsRefusedWithHttpReturnedError`, Surl commit `489f8d6f9b80614937f188b1a41896b8c5ed0e5c`.
- **Command lines:** `curl -sS --negotiate -u tester:secret http://127.0.0.1:<port>/hello.txt` and `curl -sS -f --negotiate -u tester:wrong http://127.0.0.1:<port>/hello.txt`, against `surl --auth negotiate --user-file <tester:secret> http://127.0.0.1:0/`.
- **Upstream curl 8.21.0 (correct):** static OpenSSL build, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648` (8.21.0 as tagged, which still offers NTLM inside SPNEGO): right password, exit 0, `hello from surl`; wrong password, exit 22, `curl: (22) The requested URL returned error: 401`.
- **Port (defect):** right password, exit 0 with nothing written (it took the 401 as the answer); wrong password, exit 22, `curl: (22) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`.
- **What decides:** RFC 4559 section 4 and RFC 4178 (SPNEGO may negotiate NTLMSSP). The port behaves as the Windows reference build does, which carries upstream's later `!ntlm` change; against 8.21.0 as tagged it differs. Which upstream behaviour the port follows is its maintainer's call, but exiting 0 with nothing written after a 401 is wrong either way.
- **Where to start:** `Curl.Authentication.UnitLibrary/SspiNegotiateSecurityContext.cs`.

## Acceptance criteria

- [x] With `--negotiate -u tester:secret` against a server offering `Negotiate` and no Kerberos realm, the port completes an NTLMSSP-in-SPNEGO login and writes the body, shown by a port test; or a port ADR records that the port follows the `!ntlm` behaviour, naming the upstream change.

## Notes

Decision: the port follows the Windows reference build's `!ntlm` change (ADR-1273, confirming ADR-0176); the criterion's ADR alternative is met. No code change.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 132e44e6.
- 2026-11-16: Doing -> Done. ADR-1273 records that the port follows the Windows reference build's !ntlm change
