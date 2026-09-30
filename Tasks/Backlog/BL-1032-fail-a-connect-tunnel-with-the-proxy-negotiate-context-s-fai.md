---
id: BL-1032
title: Fail a CONNECT tunnel with the proxy Negotiate context's failure as curl's error message
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-604]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1032 — Fail a CONNECT tunnel with the proxy Negotiate context's failure as curl's error message

## Goal

A CONNECT tunnel refused with `407` after a `--proxy-negotiate` context failed ends with curl 8.21.0's error message, the context's failure line (`curl: (7) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package` on Windows), not `CONNECT tunnel failed, response 407`.

## Context

- Measured in BL-604's Notes (curl 8.21.0, mingw, SSPI, 2026-09-30): `curl -s -S -v -p -x http://127.0.0.1:18606 --proxy-negotiate -U : http://example.test/` against a proxy answering the CONNECT `407` with `Proxy-Authenticate: Negotiate` writes `* CONNECT tunnel failed, response 407` under `-v` but ends `curl: (7) InitializeSecurityContext failed: ...`, exit 7, because libcurl's error buffer keeps the first `failf` of the transfer. Measure the OpenSSL/GSS-API build too (WSL) before pinning its line.
- `TcpConnector.TunnelFailure` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) makes the message; the failure lines come from `NegotiateFailureLines` through `HttpAuthRequest.Events` in `NegotiateHttpAuthenticator`. ADR-0270 records the gap.
- The forward proxy (HTTP handler) ends such a transfer with exit 0 and the 407, so only the tunnel is affected.

## Acceptance criteria

- [ ] The Windows case above is measured with `Record-CurlExchange.ps1 -Script`, and a non-Windows build's too; request bytes, stderr and exit code in Notes.
- [ ] A `Curl.Networking.UnitTests` test per platform (`OSCondition`) pins the tunnel's exit 7 and the failure line as `ConnectResult.ErrorMessage`, with a fake token source answering `NoCredentials`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-30: Created.
